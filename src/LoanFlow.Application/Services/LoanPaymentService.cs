using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── Interface ────────────────────────────────────────────────────────────────

public interface ILoanPaymentService
{
    Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest request, int userId);
    Task<Result> ApplySuspenseAsync(int suspenseId, int userId);
    Task<Result<EarlySettlePreviewDto>> PreviewEarlySettleAsync(int loanId, DateOnly settlementDate);
    Task<Result> EarlySettleAsync(int loanId, EarlySettleRequest request, int userId);
}

// ── Implementation ───────────────────────────────────────────────────────────

/// <summary>
/// Tất cả operations liên quan đến thu tiền và tất toán khoản vay.
/// Tách ra từ LoanService (56KB) để giảm xuống ~20KB mỗi service.
/// Waterfall allocation: LateFee → Interest → Principal → Suspense.
/// </summary>
public class LoanPaymentService : ILoanPaymentService
{
    private readonly LoanFlowDbContext      _db;
    private readonly ILateFeeService        _lateFeeService;
    private readonly IBusinessClock         _businessClock;
    private readonly IAccountingService     _accountingService;

    public LoanPaymentService(
        LoanFlowDbContext db,
        ILateFeeService lateFeeService,
        IBusinessClock businessClock,
        IAccountingService accountingService)
    {
        _db                = db;
        _lateFeeService    = lateFeeService;
        _businessClock     = businessClock;
        _accountingService = accountingService;
    }

    // ── POST /loans/{id}/payments ─────────────────────────────────────────────

    public async Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest req, int userId)
    {
        // EOD lock check
        var eodRunning = await _db.SystemConfigs.FirstOrDefaultAsync(s => s.Key == "IsEodRunning");
        if (eodRunning?.Value == "true")
            return Result.Failure("Hệ thống đang chạy Back-end cuối ngày (EOD). Không thể xử lý giao dịch lúc này.");

        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure("Loan not found.");

        // PAY-VAL-001: loan must be ACTIVE or OVERDUE
        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Overdue)
            return Result.Failure("PAY-VAL-001: Loan is not in payable status.");

        // PAY-VAL-002: amount > 0
        if (req.Amount <= 0)
            return Result.Failure("PAY-VAL-002: Payment amount must be greater than zero.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Make sure late fees are calculated up to payment date
            await _lateFeeService.CalculateLateFeesAsync(loanId, req.PaymentDate);

            // PAY-VAL-003: cap overpayment
            var scheduleDue = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.RemainingDueAmount);
            var lateFeeDue = loan.LateFees.Where(f => f.Status == LateFeeStatus.Pending).Sum(f => f.FeeAmount);
            var totalDue   = scheduleDue + lateFeeDue;

            // HIGH-001: Allow up to 200% of total due (supports advance payments), floor 10M VND
            var maxAllowed = Math.Max(totalDue * 2m, 10_000_000m);
            if (req.Amount > maxAllowed && totalDue > 0)
                return Result.Failure($"PAY-VAL-004: Amount ({req.Amount:N0}) exceeds maximum allowed ({maxAllowed:N0}). Use Early Settlement for full payoff.");

            // ── §13.6 Waterfall allocation: LateFee → Interest → Principal ────────
            var remaining         = req.Amount;
            decimal totalLateFee  = 0;
            decimal totalInterest = 0;
            decimal totalPrincipal = 0;
            var scheduleAllocations = new List<(int ScheduleId, decimal InterestAmount, decimal PrincipalAmount)>();

            // 1. Settle pending Late Fees first
            var pendingFees = loan.LateFees.Where(f => f.Status == LateFeeStatus.Pending).ToList();
            foreach (var fee in pendingFees)
            {
                if (remaining <= 0) break;
                if (remaining >= fee.FeeAmount)
                {
                    fee.Status  = LateFeeStatus.Paid;
                    remaining  -= fee.FeeAmount;
                    totalLateFee += fee.FeeAmount;
                }
            }

            // 2. Interest → Principal per schedule (oldest first)
            var unpaidSchedules = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .OrderBy(s => s.InstallmentNo)
                .ToList();

            foreach (var schedule in unpaidSchedules)
            {
                if (remaining <= 0) break;

                var interestDue = schedule.InterestDue - schedule.InterestPaid;
                var interestPay = Math.Min(remaining, interestDue);
                schedule.InterestPaid += interestPay;
                remaining             -= interestPay;
                totalInterest         += interestPay;

                decimal principalPay = 0;
                if (remaining > 0)
                {
                    var principalDue = schedule.PrincipalDue - schedule.PrincipalPaid;
                    principalPay           = Math.Min(remaining, principalDue);
                    schedule.PrincipalPaid += principalPay;
                    remaining              -= principalPay;
                    totalPrincipal         += principalPay;
                }

                if (interestPay > 0 || principalPay > 0)
                    scheduleAllocations.Add((schedule.Id, interestPay, principalPay));

                schedule.RemainingDueAmount = (schedule.PrincipalDue - schedule.PrincipalPaid)
                                            + (schedule.InterestDue  - schedule.InterestPaid);
                schedule.Status = schedule.RemainingDueAmount <= 0
                    ? InstallmentStatus.Paid
                    : InstallmentStatus.Partial;
            }

            var overpayment = remaining; // whatever remains → suspense

            // ── §13.3 Update loan outstanding ────────────────────────────────────
            loan.OutstandingAmount = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.RemainingDueAmount);

            // §11.2: Status transitions
            if (loan.RepaymentSchedules.All(s => s.Status == InstallmentStatus.Paid))
            {
                loan.Status = LoanStatus.Closed;
            }
            else if (loan.Status == LoanStatus.Overdue)
            {
                var today     = _businessClock.TodayBusinessDate();
                var hasOverdue = loan.RepaymentSchedules.Any(s =>
                    s.Status != InstallmentStatus.Paid && s.DueDate < today);
                if (!hasOverdue) loan.Status = LoanStatus.Active;
            }

            loan.UpdatedAt = _businessClock.UtcNow;

            // ── Persist Payment ───────────────────────────────────────────────────
            var payment = new Payment
            {
                LoanId              = loanId,
                PaymentDate         = req.PaymentDate,
                Amount              = req.Amount,
                PaymentMethod       = req.PaymentMethod,
                Remarks             = req.Remarks,
                ExternalReferenceId = req.ExternalReferenceId,
                CreatedByUserId     = userId,
                CreatedAt           = _businessClock.UtcNow
            };
            _db.Payments.Add(payment);

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified by another transaction while recording payment. Reload and retry."); }

            var loanTx = new LoanTransaction
            {
                LoanId          = loanId,
                PaymentId       = payment.Id,
                TransactionType = "Payment",
                Amount          = req.Amount,
                CreatedAt       = _businessClock.UtcNow
            };
            _db.LoanTransactions.Add(loanTx);

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified by another transaction while recording payment. Reload and retry."); }

            // ── Ledger Chain: PaymentAllocation per component ────────────────────
            var paidFees = pendingFees.Where(f => f.Status == LateFeeStatus.Paid).ToList();
            foreach (var fee in paidFees)
            {
                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId         = payment.Id,
                    LoanTransactionId = loanTx.Id,
                    ScheduleId        = fee.ScheduleId,
                    LateFeeId         = fee.Id,
                    AllocationType    = "LateFee",
                    Amount            = fee.FeeAmount
                });
            }

            foreach (var alloc in scheduleAllocations)
            {
                if (alloc.InterestAmount > 0)
                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        PaymentId         = payment.Id,
                        LoanTransactionId = loanTx.Id,
                        ScheduleId        = alloc.ScheduleId,
                        AllocationType    = "Interest",
                        Amount            = alloc.InterestAmount
                    });

                if (alloc.PrincipalAmount > 0)
                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        PaymentId         = payment.Id,
                        LoanTransactionId = loanTx.Id,
                        ScheduleId        = alloc.ScheduleId,
                        AllocationType    = "Principal",
                        Amount            = alloc.PrincipalAmount
                    });
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId   = userId,
                ActionCode    = AuditActions.PaymentRecorded,
                EntityType    = "Payment",
                EntityId      = payment.Id,
                NewValuesJson = AuditHelper.ToJson(new { amount = req.Amount, loanId })
            });

            // ── Accounting Journal Entry ──────────────────────────────────────────
            var cashCode = await _accountingService.GetAccountCodeAsync("Repayment_Principal", true);
            var prinCode = await _accountingService.GetAccountCodeAsync("Repayment_Principal", false);
            var intCode  = await _accountingService.GetAccountCodeAsync("Repayment_Interest",  false);
            var feeCode  = await _accountingService.GetAccountCodeAsync("Repayment_LateFee",   false);
            var suspCode = await _accountingService.GetAccountCodeAsync("Overpayment_Suspense", false);

            var journalLines = new List<JournalEntryLineDto>
            {
                new() { AccountCode = cashCode, DebitAmount = req.Amount, CreditAmount = 0, LineDescription = "Cash Inflow" }
            };

            if (totalPrincipal > 0)
                journalLines.Add(new() { AccountCode = prinCode, DebitAmount = 0, CreditAmount = totalPrincipal, LineDescription = "Principal Repayment" });
            if (totalInterest > 0)
                journalLines.Add(new() { AccountCode = intCode, DebitAmount = 0, CreditAmount = totalInterest, LineDescription = "Interest Receivable Repayment" });
            if (totalLateFee > 0)
                journalLines.Add(new() { AccountCode = feeCode, DebitAmount = 0, CreditAmount = totalLateFee, LineDescription = "Late Fee Repayment" });
            if (overpayment > 0)
                journalLines.Add(new() { AccountCode = suspCode, DebitAmount = 0, CreditAmount = overpayment, LineDescription = "Overpayment to Suspense" });

            var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
            {
                ReferenceNumber = $"PAY-{payment.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                BusinessDate    = req.PaymentDate,
                Description     = $"Payment Recorded for Loan {loanId}",
                Lines           = journalLines
            });

            if (!journalResult.IsSuccess)
            {
                await tx.RollbackAsync();
                throw new DomainValidationException($"Accounting failed: {journalResult.Error}");
            }

            // ── Overpayment → Suspense ────────────────────────────────────────────
            if (overpayment > 0)
            {
                var nextSchedule = loan.RepaymentSchedules
                    .Where(s => s.Status != InstallmentStatus.Paid)
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefault();

                var suspenseReason = nextSchedule != null && overpayment >= nextSchedule.RemainingDueAmount
                    ? "Advance Payment"
                    : "Overpayment (Suspense)";

                _db.SuspenseTransactions.Add(new SuspenseTransaction
                {
                    LoanId                  = loanId,
                    Amount                  = overpayment,
                    Reason                  = suspenseReason,
                    SourcePaymentId         = payment.Id,
                    SourceLoanTransactionId = loanTx.Id
                });

                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId         = payment.Id,
                    LoanTransactionId = loanTx.Id,
                    ScheduleId        = null,
                    AllocationType    = "Suspense",
                    Amount            = overpayment
                });

                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId   = userId,
                    ActionCode    = AuditActions.SuspenseCreated,
                    EntityType    = "Loan",
                    EntityId      = loanId,
                    NewValuesJson = AuditHelper.ToJson(new { amount = overpayment })
                });
            }

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified by another transaction while recording payment. Reload and retry."); }

            await tx.CommitAsync();
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            throw new ConcurrencyConflictException("Loan was modified by another transaction while recording payment. Reload and retry.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── POST /loans/suspense/{id}/apply ──────────────────────────────────────

    public async Task<Result> ApplySuspenseAsync(int suspenseId, int userId)
    {
        var suspense = await _db.SuspenseTransactions
            .Include(s => s.Loan)
            .FirstOrDefaultAsync(s => s.Id == suspenseId);

        if (suspense is null) return Result.Failure("Suspense transaction not found.");
        if (suspense.Status != SuspenseStatus.Pending) return Result.Failure("Suspense is not pending.");

        var applyReq = new RecordPaymentRequest
        {
            PaymentDate   = _businessClock.TodayBusinessDate(),
            Amount        = suspense.Amount,
            PaymentMethod = "SuspenseAccount",
            Remarks       = "Applied from Suspense Account"
        };

        var result = await RecordPaymentAsync(suspense.LoanId, applyReq, userId);
        if (result.IsSuccess)
        {
            suspense.Status          = SuspenseStatus.Applied;
            suspense.AppliedAt       = _businessClock.TodayBusinessDate();
            suspense.AppliedByUserId = userId;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId   = userId,
                ActionCode    = AuditActions.SuspenseApplied,
                EntityType    = "SuspenseTransaction",
                EntityId      = suspenseId,
                NewValuesJson = AuditHelper.ToJson(new { amount = suspense.Amount, loanId = suspense.LoanId })
            });

            await _db.SaveChangesAsync();
        }
        return result;
    }

    // ── EARLY SETTLEMENT ─────────────────────────────────────────────────────

    public async Task<Result<EarlySettlePreviewDto>> PreviewEarlySettleAsync(int loanId, DateOnly settlementDate)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure<EarlySettlePreviewDto>("Loan not found.");
        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Overdue)
            return Result.Failure<EarlySettlePreviewDto>("Loan is not in active/overdue status.");

        var unpaidSchedules = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .OrderBy(s => s.InstallmentNo)
            .ToList();

        // 1. Remaining principal
        var remainingPrincipal = unpaidSchedules.Sum(s => s.PrincipalDue - s.PrincipalPaid);

        // 2. Pro-rata interest for current installment period
        var currentInstallment = unpaidSchedules.FirstOrDefault(s => s.DueDate >= settlementDate);
        decimal proRataInterest = 0;

        if (currentInstallment != null)
        {
            var previousDueDate = currentInstallment.InstallmentNo == 1
                ? loan.DisbursementDate!.Value
                : loan.RepaymentSchedules.First(s => s.InstallmentNo == currentInstallment.InstallmentNo - 1).DueDate;

            int daysInPeriod = currentInstallment.DueDate.DayNumber - previousDueDate.DayNumber;
            int daysElapsed  = settlementDate.DayNumber - previousDueDate.DayNumber;

            if (daysElapsed > 0 && daysElapsed <= daysInPeriod)
                proRataInterest = Math.Round(currentInstallment.InterestDue * ((decimal)daysElapsed / daysInPeriod), 2);
            else if (daysElapsed > daysInPeriod)
                proRataInterest = currentInstallment.InterestDue;

            proRataInterest -= currentInstallment.InterestPaid;
            if (proRataInterest < 0) proRataInterest = 0;
        }

        // Add interest from all past overdue installments
        proRataInterest += unpaidSchedules
            .Where(s => s.DueDate < settlementDate)
            .Sum(s => s.InterestDue - s.InterestPaid);

        // 3. Early Settlement Fee
        var earlySettlementFee = Math.Round(remainingPrincipal * loan.EarlySettlementFeeRate, 2);

        // 4. Unpaid Late Fees
        var unpaidLateFees = loan.LateFees.Where(f => f.Status == LateFeeStatus.Pending).Sum(f => f.FeeAmount);

        var total = remainingPrincipal + proRataInterest + earlySettlementFee + unpaidLateFees;

        return Result.Success(new EarlySettlePreviewDto
        {
            SettlementDate        = settlementDate,
            RemainingPrincipal    = remainingPrincipal,
            ProRataInterest       = proRataInterest,
            EarlySettlementFee    = earlySettlementFee,
            UnpaidLateFees        = unpaidLateFees,
            TotalSettlementAmount = total
        });
    }

    public async Task<Result> EarlySettleAsync(int loanId, EarlySettleRequest request, int userId)
    {
        // EOD lock check
        var eodRunning = await _db.SystemConfigs.FirstOrDefaultAsync(s => s.Key == "IsEodRunning");
        if (eodRunning?.Value == "true")
            return Result.Failure("Hệ thống đang chạy Back-end cuối ngày (EOD). Không thể xử lý giao dịch lúc này.");

        var previewResult = await PreviewEarlySettleAsync(loanId, request.SettlementDate);
        if (!previewResult.IsSuccess) return Result.Failure(previewResult.Error!);

        var preview     = previewResult.Value!;
        var totalAmount = preview.TotalSettlementAmount;

        if (request.WaiveFee)
        {
            totalAmount           -= preview.EarlySettlementFee;
            preview.EarlySettlementFee = 0;
        }

        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == loanId);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Mark all late fees as paid
            foreach (var fee in loan!.LateFees.Where(f => f.Status == LateFeeStatus.Pending))
                fee.Status = LateFeeStatus.Paid;

            // Mark all schedules as paid (with pro-rata adjustment)
            foreach (var s in loan.RepaymentSchedules.Where(s => s.Status != InstallmentStatus.Paid))
            {
                s.PrincipalPaid = s.PrincipalDue;
                if (s.DueDate >= request.SettlementDate)
                {
                    s.InterestDue = s.InterestPaid = s == loan.RepaymentSchedules
                        .FirstOrDefault(x => x.DueDate >= request.SettlementDate)
                        ? preview.ProRataInterest
                        : 0;
                }
                else
                {
                    s.InterestPaid = s.InterestDue;
                }
                s.RemainingDueAmount = 0;
                s.Status             = InstallmentStatus.Paid;
            }

            loan.OutstandingAmount = 0;
            loan.Status            = LoanStatus.Closed;
            loan.UpdatedAt         = _businessClock.UtcNow;

            var payment = new Payment
            {
                LoanId          = loanId,
                PaymentDate     = request.SettlementDate,
                Amount          = totalAmount,
                PaymentMethod   = "BankTransfer",
                Remarks         = $"Early Settlement. Fee Waived: {request.WaiveFee}",
                CreatedByUserId = userId
            };
            _db.Payments.Add(payment);

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified while settling early. Reload and retry."); }

            _db.LoanTransactions.Add(new LoanTransaction
            {
                LoanId = loanId, PaymentId = payment.Id,
                TransactionType = "EarlySettlement", Amount = totalAmount
            });
            if (preview.EarlySettlementFee > 0)
                _db.LoanTransactions.Add(new LoanTransaction
                {
                    LoanId = loanId, PaymentId = payment.Id,
                    TransactionType = "EarlySettlementFee", Amount = preview.EarlySettlementFee
                });

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId   = userId,
                ActionCode    = AuditActions.LoanEarlySettled,
                EntityType    = "Loan",
                EntityId      = loanId,
                NewValuesJson = AuditHelper.ToJson(new { settlementAmount = totalAmount, feeWaived = request.WaiveFee })
            });

            // ── Accounting Journal Entry ──────────────────────────────────────────
            var esCashCode  = await _accountingService.GetAccountCodeAsync("Repayment_Principal",      true);
            var esPrinCode  = await _accountingService.GetAccountCodeAsync("Repayment_Principal",      false);
            var esIntCode   = await _accountingService.GetAccountCodeAsync("Repayment_Interest",       false);
            var esFeeCode   = await _accountingService.GetAccountCodeAsync("Repayment_LateFee",        false);
            var esEarlyCode = await _accountingService.GetAccountCodeAsync("Repayment_EarlySettleFee", false);

            var journalLines = new List<JournalEntryLineDto>
            {
                new() { AccountCode = esCashCode, DebitAmount = totalAmount, CreditAmount = 0, LineDescription = "Cash Inflow from Early Settle" }
            };
            if (preview.RemainingPrincipal > 0)
                journalLines.Add(new() { AccountCode = esPrinCode, DebitAmount = 0, CreditAmount = preview.RemainingPrincipal, LineDescription = "Principal Repaid" });
            if (preview.ProRataInterest > 0)
                journalLines.Add(new() { AccountCode = esIntCode, DebitAmount = 0, CreditAmount = preview.ProRataInterest, LineDescription = "Interest Repaid" });
            if (preview.UnpaidLateFees > 0)
                journalLines.Add(new() { AccountCode = esFeeCode, DebitAmount = 0, CreditAmount = preview.UnpaidLateFees, LineDescription = "Late Fees Repaid" });
            if (preview.EarlySettlementFee > 0)
                journalLines.Add(new() { AccountCode = esEarlyCode, DebitAmount = 0, CreditAmount = preview.EarlySettlementFee, LineDescription = "Early Settlement Fee Income" });

            var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
            {
                ReferenceNumber = $"EARLYSETTLE-{payment.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                BusinessDate    = request.SettlementDate,
                Description     = $"Early Settlement for Loan {loanId}",
                Lines           = journalLines
            });

            if (!journalResult.IsSuccess)
            {
                await tx.RollbackAsync();
                throw new DomainValidationException($"Accounting failed: {journalResult.Error}");
            }

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified while settling early. Reload and retry."); }

            await tx.CommitAsync();
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            throw new ConcurrencyConflictException("Loan was modified while settling early. Reload and retry.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
