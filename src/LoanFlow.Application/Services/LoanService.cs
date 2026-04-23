using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILoanService
{
    Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter);
    Task<Result<LoanDto>> GetByIdAsync(int id);
    Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId);
    Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest request, int userId);
    Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest request, int userId);
    Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId);
    Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int loanId);
    Task<Result> AddCollectionNoteAsync(int loanId, AddCollectionNoteRequest request, int userId);
    Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync();
    // Phase 2
    Task<Result<EarlySettlePreviewDto>> PreviewEarlySettleAsync(int loanId, DateOnly settlementDate);
    Task<Result> EarlySettleAsync(int loanId, EarlySettleRequest request, int userId);
    // Wave 4
    Task<Result> ApplySuspenseAsync(int suspenseId, int userId);
}

public class LoanService : ILoanService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILateFeeService _lateFeeService;
    private readonly IBusinessClock _businessClock;
    private readonly IAccountingService _accountingService;
    private readonly IPaymentGatewayService _paymentGatewayService;

    public LoanService(LoanFlowDbContext db, ILateFeeService lateFeeService, IBusinessClock businessClock, IAccountingService accountingService, IPaymentGatewayService paymentGatewayService)
    {
        _db = db;
        _lateFeeService = lateFeeService;
        _businessClock = businessClock;
        _accountingService = accountingService;
        _paymentGatewayService = paymentGatewayService;
    }

    // ── GET /loans ──────────────────────────────────────────────────────────
    public async Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter)
    {
        var query = _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.Application)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(l => l.LoanAccountNo.Contains(s) || l.Customer.FullName.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(l => l.Status == filter.Status);
        if (filter.CustomerId.HasValue)
            query = query.Where(l => l.CustomerId == filter.CustomerId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<LoanDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    // ── GET /loans/{id} ─────────────────────────────────────────────────────
    public async Task<Result<LoanDto>> GetByIdAsync(int id)
    {
        var today = _businessClock.TodayBusinessDate();
        
        // In a true bank-grade system, Late Fees are strictly accrued during EOD Batch.
        // We DO NOT mutate the database on a GET request.
        // If we want to show real-time projections, we would project DTOs here instead.
        // For now, we rely on the EOD job to have updated the LateFees up to today.

        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.Application)
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.Payments).ThenInclude(p => p.CreatedBy)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (loan is null) return Result.Failure<LoanDto>("Loan not found.");

        var dto = MapToDto(loan);

        // Map schedules and late fees
        dto.Schedule = loan.RepaymentSchedules
            .OrderBy(s => s.InstallmentNo)
            .Select(s => {
                var item = MapSchedule(s, today);
                item.UnpaidLateFeeAmount = loan.LateFees
                    .Where(f => f.ScheduleId == s.Id && f.Status == LateFeeStatus.Pending)
                    .Sum(f => f.FeeAmount);
                return item;
            })
            .ToList();

        dto.LateFees = loan.LateFees
            .Select(f => new LateFeeDto
            {
                Id = f.Id, ScheduleId = f.ScheduleId, OverdueAmount = f.OverdueAmount,
                DaysOverdue = f.DaysOverdue, FeeRate = f.FeeRate, FeeAmount = f.FeeAmount,
                Status = f.Status, CalculatedAt = f.CalculatedAt
            })
            .ToList();

        dto.Payments = loan.Payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(MapPayment)
            .ToList();

        return Result.Success(dto);
    }

    // ── POST /applications/{id}/create-loan (§LOAN-001) ────────────────────
    public async Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId)
    {
        var application = await _db.LoanApplications
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null) return Result.Failure<LoanDto>("LOAN-VAL-001: Application not found.");

        // §12.6 Rule 1: only approved application creates loan
        if (application.Status != ApplicationStatus.Approved)
            return Result.Failure<LoanDto>("LOAN-VAL-001: Only approved application can create loan.");

        // §12.6 Rule 1: one loan only
        if (await _db.Loans.AnyAsync(l => l.ApplicationId == applicationId))
            return Result.Failure<LoanDto>("LOAN-VAL-002: Loan already created for this application.");

        var product = application.Product;
        var loanNo = await GenerateLoanAccountNoAsync();

        var loan = new Loan
        {
            LoanAccountNo = loanNo,
            ApplicationId = applicationId,
            CustomerId = application.CustomerId,
            ProductId = application.ProductId,
            ApprovedAmount = application.RequestedAmount,
            TenorMonths = application.RequestedTenorMonths,
            MonthlyInterestRate = product.MonthlyInterestRate,     // snapshot
            ProcessingFee = product.ProcessingFee,                  // snapshot
            Status = LoanStatus.PendingDisbursement,
            OutstandingAmount = application.RequestedAmount,
            // Bank-grade snapshots
            InterestMethod = product.InterestMethod,
            GracePeriodMonths = product.GracePeriodMonths,
            PromotionalRate = product.PromotionalRate,
            PromotionalMonths = product.PromotionalMonths,
            EarlySettlementFeeRate = product.EarlySettlementFeeRate,
            LateFeeRate = product.LateFeeRate,
            LateFeeGraceDays = product.LateFeeGraceDays,
            ProductType = product.ProductType
        };
        _db.Loans.Add(loan);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.LoanCreated,
            EntityType = "Loan",
            EntityId = 0, // will be updated after save
            NewValuesJson = AuditHelper.ToJson(new { loanAccountNo = loanNo, applicationId })
        });
        await _db.SaveChangesAsync(); // single roundtrip — EF resolves loan.Id
        
        // Phase 4: Generate Virtual Account Number
        loan.VirtualAccountNo = $"VA{loan.Id:D8}";
        await _db.SaveChangesAsync();

        return await GetByIdAsync(loan.Id);
    }

    private async Task EnsureSystemNotLockedAsync()
    {
        var eodRunning = await _db.SystemConfigs.FirstOrDefaultAsync(s => s.Key == "IsEodRunning");
        if (eodRunning != null && eodRunning.Value == "true")
        {
            throw new InvalidOperationException("Hệ thống đang chạy Back-end cuối ngày (EOD). Không thể xử lý giao dịch lúc này.");
        }
    }

    // ── POST /loans/{id}/disburse (§LOAN-002) ────────────────────────────────
    public async Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest req, int userId)
    {
        try 
        {
            await EnsureSystemNotLockedAsync();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<LoanDto>(ex.Message);
        }
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.LoanCollaterals)
            .FirstOrDefaultAsync(l => l.Id == loanId);
            
        if (loan is null) return Result.Failure<LoanDto>("Loan not found.");

        // DISB-VAL-001
        if (loan.Status != LoanStatus.PendingDisbursement)
            return Result.Failure<LoanDto>("DISB-VAL-001: Loan is not ready for disbursement.");

        // DISB-VAL-003
        if (await _db.LoanDisbursements.AnyAsync(d => d.LoanId == loanId))
            return Result.Failure<LoanDto>("DISB-VAL-003: Loan already disbursed.");

        // DFI-VAL-004: E-Contract Must Be Signed
        if (!await _db.EContracts.AnyAsync(e => e.LoanId == loanId))
            return Result.Failure<LoanDto>("APP-VAL-006: E-Contract is not signed. Digital signature is required before disbursement.");

        // Collateral LTV Validation (Phase 3)
        if (loan.Product != null && loan.Product.RequiresCollateral)
        {
            if (!loan.Product.MaxLtvRatio.HasValue || loan.Product.MaxLtvRatio.Value <= 0)
                return Result.Failure<LoanDto>("Product requires collateral but Max LTV Ratio is not configured correctly (> 0).");
                
            var sumAllocated = loan.LoanCollaterals.Sum(lc => lc.AllocatedValue);
            var reqCollateralValue = loan.ApprovedAmount / (loan.Product.MaxLtvRatio.Value / 100m); // MaxLtvRatio is e.g. 70 for 70%
            
            if (sumAllocated < reqCollateralValue)
            {
                return Result.Failure<LoanDto>($"LTV Validation Failed: Required collateral value is {reqCollateralValue:N0} (LTV {loan.Product.MaxLtvRatio}%), but only {sumAllocated:N0} is allocated.");
            }
        }

        // DISB-VAL-002: disbursed amount must equal approved amount (§12.5 Rule 3)
        if (req.DisbursedAmount != loan.ApprovedAmount)
            return Result.Failure<LoanDto>($"DISB-VAL-002: Disbursed amount must equal approved amount ({loan.ApprovedAmount:N0}).");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // §12.5 Rule 4: loan becomes ACTIVE
            loan.DisbursementDate = req.DisbursementDate;
            loan.MaturityDate = req.DisbursementDate.AddMonths(loan.TenorMonths);
            loan.Status = LoanStatus.Active;
            loan.UpdatedAt = _businessClock.UtcNow;

            var loanDisbursement = new LoanDisbursement
            {
                LoanId = loanId,
                DisbursedAmount = req.DisbursedAmount,
                DisbursementDate = req.DisbursementDate,
                RecordedByUserId = userId,
                DisbursementMethod = "Cash",
                CreatedAt = _businessClock.UtcNow
            };
            _db.LoanDisbursements.Add(loanDisbursement);

            var loanTx = new LoanTransaction
            {
                LoanId = loanId,
                TransactionType = "Disbursement",
                Amount = req.DisbursedAmount,
                CreatedAt = _businessClock.UtcNow
            };
            _db.LoanTransactions.Add(loanTx);

            // §12.5 Rule 5 + §13.2: Generate repayment schedule — Flat or ReducingBalance
            if (loan.InterestMethod == Domain.Constants.InterestMethod.ReducingBalance)
                GenerateReducingBalanceSchedule(loan, req.DisbursementDate);
            else
                GenerateSchedule(loan, req.DisbursementDate);

            // Phase 3: Post Disbursement Journal Entry
            var debitCode = await _accountingService.GetAccountCodeAsync("Disbursement", true);
            var creditCode = await _accountingService.GetAccountCodeAsync("Disbursement", false);

            var journalLines = new List<JournalEntryLineDto>
            {
                new JournalEntryLineDto { AccountCode = debitCode, DebitAmount = req.DisbursedAmount, CreditAmount = 0, LineDescription = "Loan Principal Disbursement" }
            };

            if (loan.ProcessingFee > 0)
            {
                var feeIncomeCode = await _accountingService.GetAccountCodeAsync("ProcessingFee", false);
                var netCashOutflow = req.DisbursedAmount - loan.ProcessingFee;
                journalLines.Add(new JournalEntryLineDto { AccountCode = creditCode, DebitAmount = 0, CreditAmount = netCashOutflow, LineDescription = "Net Cash Outflow" });
                journalLines.Add(new JournalEntryLineDto { AccountCode = feeIncomeCode, DebitAmount = 0, CreditAmount = loan.ProcessingFee, LineDescription = "Processing Fee Deduced Upfront" });
            }
            else
            {
                journalLines.Add(new JournalEntryLineDto { AccountCode = creditCode, DebitAmount = 0, CreditAmount = req.DisbursedAmount, LineDescription = "Cash Outflow" });
            }

            var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
            {
                ReferenceNumber = $"DISB-{loanId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                BusinessDate = req.DisbursementDate,
                Description = $"Disbursement for Loan {loan.LoanAccountNo}",
                Lines = journalLines
            });

            if (!journalResult.IsSuccess)
            {
                await tx.RollbackAsync();
                return Result.Failure<LoanDto>($"Accounting failed: {journalResult.Error}");
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.LoanDisbursed,
                EntityType = "Loan",
                EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new { disbursedAmount = req.DisbursedAmount, disbursementDate = req.DisbursementDate })
            });

            var isAutoDisburse = !string.IsNullOrWhiteSpace(loan.Customer.BankAccountNumber) && !string.IsNullOrWhiteSpace(loan.Customer.BankCode);
            if (isAutoDisburse)
            {
                loanDisbursement.DisbursementMethod = "BankTransfer";
            }

            await SaveChangesWithConcurrencyHandlingAsync("Loan was modified while disbursing. Reload the loan and retry.");
            await tx.CommitAsync();

            // Phase 4: Auto-Disbursement (Sync via NAPAS Gateway) AFTER TX Commit
            if (isAutoDisburse)
            {
                var gatewayResult = await _paymentGatewayService.TransferToCustomerAsync(
                    loan.Customer.BankCode,
                    loan.Customer.BankAccountNumber,
                    req.DisbursedAmount,
                    $"Disbursement for Loan {loan.LoanAccountNo}"
                );

                if (!gatewayResult.IsSuccess)
                {
                    // Compensating action
                    loanDisbursement.DisbursementMethod = "Failed";
                    var reversal = new TransactionReversal
                    {
                        LoanId = loanId,
                        OriginalTransactionId = loanTx.Id,
                        ReversalType = "DisbursementCorrection",
                        OriginalAmount = req.DisbursedAmount,
                        ReversedAmount = 0,
                        Reason = "Auto-Disbursement Gateway Failed: " + gatewayResult.Error,
                        Status = "Pending",
                        RequestedByUserId = userId
                    };
                    _db.TransactionReversals.Add(reversal);

                    _db.AuditLogs.Add(new AuditLog
                    {
                        ActorUserId = userId,
                        ActionCode = "AutoDisbursementFailed",
                        EntityType = "Loan",
                        EntityId = loanId,
                        NewValuesJson = AuditHelper.ToJson(new { error = gatewayResult.Error })
                    });

                    await _db.SaveChangesAsync();
                    return Result.Failure<LoanDto>($"Auto-Disbursement Gateway Failed: {gatewayResult.Error}. Disburse record marked as failed. Reversal requested.");
                }
                else
                {
                    _db.AuditLogs.Add(new AuditLog
                    {
                        ActorUserId = userId,
                        ActionCode = "AutoDisbursementSuccess",
                        EntityType = "Loan",
                        EntityId = loanId,
                        NewValuesJson = AuditHelper.ToJson(new { transactionId = gatewayResult.Value.TransactionId })
                    });
                    await _db.SaveChangesAsync();
                }
            }

            return await GetByIdAsync(loanId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync();
            throw new ConcurrencyConflictException("Loan was modified while disbursing. Reload the loan and retry.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── POST /loans/{id}/payments (§PAY-001) ─────────────────────────────────
    public async Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest req, int userId)
    {
        try 
        {
            await EnsureSystemNotLockedAsync();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

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

        // PAY-VAL-003: amount <= total outstanding due + unpaid late fees
        var scheduleDue = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .Sum(s => s.RemainingDueAmount);
            
        var lateFeeDue = loan.LateFees.Where(f => f.Status == LateFeeStatus.Pending).Sum(f => f.FeeAmount);
        var totalDue = scheduleDue + lateFeeDue;

        // HIGH-001: Cap overpayment to prevent teller typo creating massive suspense
        // Allow up to 200% of total due (supports advance payments), minimum floor 10M VND
        var maxAllowed = Math.Max(totalDue * 2m, 10_000_000m);
        if (req.Amount > maxAllowed && totalDue > 0)
            return Result.Failure($"PAY-VAL-004: Amount ({req.Amount:N0}) exceeds maximum allowed ({maxAllowed:N0}). Use Early Settlement for full payoff.");

        // §13.6 Payment allocation: LateFee → Interest → Principal
        var remaining = req.Amount; // Unallocated cash pool
        decimal totalLateFeeToPay = 0;
        decimal totalInterestToPay = 0;
        decimal totalPrincipalToPay = 0;
        var scheduleAllocations = new List<(int ScheduleId, decimal InterestAmount, decimal PrincipalAmount)>();
        
        // 1. Pay pending Late Fees first
        var pendingFees = loan.LateFees.Where(f => f.Status == LateFeeStatus.Pending).ToList();
        foreach(var fee in pendingFees)
        {
            if (remaining <= 0) break;
            
            // Assuming partial late fee payment isn't supported in entity (it's either Pending or Paid),
            // in reality we might need partial fee tracking. For MVP, we fully pay if amount covers.
            // But if it doesn't cover fully, we deduct but keep pending. Since entity has no 'FeePaid',
            // let's simplify: if payment >= fee, mark paid. Otherwise skip (cannot partial pay fee).
            if (remaining >= fee.FeeAmount)
            {
                fee.Status = LateFeeStatus.Paid;
                remaining -= fee.FeeAmount;
                totalLateFeeToPay += fee.FeeAmount;
            }
        }

        var unpaidSchedules = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .OrderBy(s => s.InstallmentNo)
            .ToList();

        foreach (var schedule in unpaidSchedules)
        {
            if (remaining <= 0) break;

            // 2. Interest
            var interestDue = schedule.InterestDue - schedule.InterestPaid;
            var interestPay = Math.Min(remaining, interestDue);
            schedule.InterestPaid += interestPay;
            remaining -= interestPay;
            totalInterestToPay += interestPay;

            decimal principalPay = 0;

            // Then principal
            if (remaining > 0)
            {
                var principalDue = schedule.PrincipalDue - schedule.PrincipalPaid;
                principalPay = Math.Min(remaining, principalDue);
                schedule.PrincipalPaid += principalPay;
                remaining -= principalPay;
                totalPrincipalToPay += principalPay;
            }

            if (interestPay > 0 || principalPay > 0)
            {
                scheduleAllocations.Add((schedule.Id, interestPay, principalPay));
            }

            // Update remaining due and status
            schedule.RemainingDueAmount = (schedule.PrincipalDue - schedule.PrincipalPaid)
                                        + (schedule.InterestDue - schedule.InterestPaid);

            schedule.Status = schedule.RemainingDueAmount <= 0
                ? InstallmentStatus.Paid
                : InstallmentStatus.Partial;
        }

        var overpayment = remaining; // Whatever is left becomes overpayment/suspense

        // §13.3 Update loan outstanding = sum of remaining due
        loan.OutstandingAmount = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .Sum(s => s.RemainingDueAmount);

        // §11.2: ACTIVE → CLOSED if all paid
        if (loan.RepaymentSchedules.All(s => s.Status == InstallmentStatus.Paid))
        {
            loan.Status = LoanStatus.Closed;
        }
        // §11.2: OVERDUE → ACTIVE if all overdue installments paid
        else if (loan.Status == LoanStatus.Overdue)
        {
            var today = _businessClock.TodayBusinessDate();
            var hasOverdue = loan.RepaymentSchedules.Any(s =>
                s.Status != InstallmentStatus.Paid && s.DueDate < today);
            if (!hasOverdue) loan.Status = LoanStatus.Active;
        }

        loan.UpdatedAt = _businessClock.UtcNow;

        var payment = new Payment
        {
            LoanId = loanId,
            PaymentDate = req.PaymentDate,
            Amount = req.Amount,
            PaymentMethod = req.PaymentMethod,
            Remarks = req.Remarks,
            ExternalReferenceId = req.ExternalReferenceId,
            CreatedByUserId = userId,
            CreatedAt = _businessClock.UtcNow
        };
        _db.Payments.Add(payment);
        await SaveChangesWithConcurrencyHandlingAsync("Loan was modified by another transaction while recording payment. Reload and retry.");

        var loanTx = new LoanTransaction
        {
            LoanId = loanId,
            PaymentId = payment.Id,
            TransactionType = "Payment",
            Amount = req.Amount,
            CreatedAt = _businessClock.UtcNow
        };
        _db.LoanTransactions.Add(loanTx);
        await SaveChangesWithConcurrencyHandlingAsync("Loan was modified by another transaction while recording payment. Reload and retry.");

        // ── Ledger Chain: write PaymentAllocation per component ──────────────
        // LateFee allocations
        var paidFees = pendingFees.Where(f => f.Status == LateFeeStatus.Paid).ToList();
        foreach (var fee in paidFees)
        {
            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                PaymentId = payment.Id,
                LoanTransactionId = loanTx.Id,
                ScheduleId = fee.ScheduleId,
                LateFeeId = fee.Id,
                AllocationType = "LateFee",
                Amount = fee.FeeAmount
            });
        }

        // Principal/Interest allocations per schedule
        foreach (var allocation in scheduleAllocations)
        {
            if (allocation.InterestAmount > 0)
                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    LoanTransactionId = loanTx.Id,
                    ScheduleId = allocation.ScheduleId,
                    AllocationType = "Interest",
                    Amount = allocation.InterestAmount
                });

            if (allocation.PrincipalAmount > 0)
                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    LoanTransactionId = loanTx.Id,
                    ScheduleId = allocation.ScheduleId,
                    AllocationType = "Principal",
                    Amount = allocation.PrincipalAmount
                });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.PaymentRecorded,
            EntityType = "Payment",
            EntityId = payment.Id,
            NewValuesJson = AuditHelper.ToJson(new { amount = req.Amount, loanId })
        });

        // Phase 3: Post Payment Journal Entry
        var cashCode = await _accountingService.GetAccountCodeAsync("Repayment_Principal", true);
        var prinCode = await _accountingService.GetAccountCodeAsync("Repayment_Principal", false);
        var intCode = await _accountingService.GetAccountCodeAsync("Repayment_Interest", false);
        var feeCode = await _accountingService.GetAccountCodeAsync("Repayment_LateFee", false);
        var suspCode = await _accountingService.GetAccountCodeAsync("Overpayment_Suspense", false);

        var journalLines = new List<JournalEntryLineDto>
        {
            new JournalEntryLineDto { AccountCode = cashCode, DebitAmount = req.Amount, CreditAmount = 0, LineDescription = "Cash Inflow" }
        };

        if (totalPrincipalToPay > 0)
        {
            journalLines.Add(new JournalEntryLineDto { AccountCode = prinCode, DebitAmount = 0, CreditAmount = totalPrincipalToPay, LineDescription = "Principal Repayment" });
        }

        if (totalInterestToPay > 0)
        {
            journalLines.Add(new JournalEntryLineDto { AccountCode = intCode, DebitAmount = 0, CreditAmount = totalInterestToPay, LineDescription = "Interest Receivable Repayment" });
        }
        
        if (totalLateFeeToPay > 0)
        {
            journalLines.Add(new JournalEntryLineDto { AccountCode = feeCode, DebitAmount = 0, CreditAmount = totalLateFeeToPay, LineDescription = "Late Fee Repayment" });
        }

        if (overpayment > 0)
        {
            journalLines.Add(new JournalEntryLineDto { AccountCode = suspCode, DebitAmount = 0, CreditAmount = overpayment, LineDescription = "Overpayment to Suspense" });
        }

        var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
        {
            ReferenceNumber = $"PAY-{payment.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            BusinessDate = req.PaymentDate,
            Description = $"Payment Recorded for Loan {loan.Id}",
            Lines = journalLines
        });

        if (!journalResult.IsSuccess)
        {
            await tx.RollbackAsync();
            throw new InvalidOperationException($"Accounting failed: {journalResult.Error}");
        }

        // Wave 4 / CF4: Update Suspense vs Advance Payment rule
        if (overpayment > 0)
        {
            var nextSchedule = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .OrderBy(s => s.InstallmentNo)
                .FirstOrDefault();

            string suspenseReason = "Overpayment (Suspense)";
            
            if (nextSchedule != null && overpayment >= nextSchedule.RemainingDueAmount)
            {
                suspenseReason = "Advance Payment";
            }

            var suspense = new SuspenseTransaction
            {
                LoanId = loanId,
                Amount = overpayment,
                Reason = suspenseReason,
                // Ledger chain: trace back to the originating payment
                SourcePaymentId = payment.Id,
                SourceLoanTransactionId = loanTx.Id
            };
            _db.SuspenseTransactions.Add(suspense);

            // PaymentAllocation entry for the suspense portion
            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                PaymentId = payment.Id,
                LoanTransactionId = loanTx.Id,
                ScheduleId = null,
                AllocationType = "Suspense",
                Amount = overpayment
            });
            
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.SuspenseCreated,
                EntityType = "Loan",
                EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new { amount = overpayment })
            });
        }

        await SaveChangesWithConcurrencyHandlingAsync("Loan was modified by another transaction while recording payment. Reload and retry.");
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

    // ── POST /loans/suspense/{id}/apply ─────────────────────────────────────
    public async Task<Result> ApplySuspenseAsync(int suspenseId, int userId)
    {
        var suspense = await _db.SuspenseTransactions
            .Include(s => s.Loan)
            .FirstOrDefaultAsync(s => s.Id == suspenseId);

        if (suspense == null) return Result.Failure("Suspense transaction not found.");
        if (suspense.Status != SuspenseStatus.Pending) return Result.Failure("Suspense is not pending.");

        // Here we simulate applying suspense by recording a new payment
        var applyReq = new RecordPaymentRequest
        {
            PaymentDate = _businessClock.TodayBusinessDate(),
            Amount = suspense.Amount,
            PaymentMethod = "SuspenseAccount",
            Remarks = "Applied from Suspense Account"
        };

        var result = await RecordPaymentAsync(suspense.LoanId, applyReq, userId);
        if (result.IsSuccess)
        {
            suspense.Status = SuspenseStatus.Applied;
            suspense.AppliedAt = _businessClock.TodayBusinessDate();
            suspense.AppliedByUserId = userId;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.SuspenseApplied,
                EntityType = "SuspenseTransaction",
                EntityId = suspenseId,
                NewValuesJson = AuditHelper.ToJson(new { amount = suspense.Amount, loanId = suspense.LoanId })
            });

            await _db.SaveChangesAsync();
        }
        return result;
    }

    // ── EARLY SETTLEMENT (Phase 2) ──────────────────────────────────────────

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

        // Note: For a true Bank-grade system, we do not mutate DB on Preview.
        // We rely on the EOD job or project it in-memory.
        // For now, we use the LateFees already in the DB (accrued up to yesterday).

        var unpaidSchedules = loan.RepaymentSchedules.Where(s => s.Status != InstallmentStatus.Paid).OrderBy(s => s.InstallmentNo).ToList();
        
        // 1. Remaining principal is sum of all unpaid principal
        var remainingPrincipal = unpaidSchedules.Sum(s => s.PrincipalDue - s.PrincipalPaid);
        
        // 2. Pro-rata interest: interest of the CURRENT installment up to settlement date
        // Find the installment that contains the settlement date
        var currentInstallment = unpaidSchedules.FirstOrDefault(s => s.DueDate >= settlementDate);
        decimal proRataInterest = 0;
        
        if (currentInstallment != null)
        {
            var previousDueDate = currentInstallment.InstallmentNo == 1 
                ? loan.DisbursementDate!.Value 
                : loan.RepaymentSchedules.First(s => s.InstallmentNo == currentInstallment.InstallmentNo - 1).DueDate;
                
            int daysInPeriod = currentInstallment.DueDate.DayNumber - previousDueDate.DayNumber;
            int daysElapsed = settlementDate.DayNumber - previousDueDate.DayNumber;
            
            if (daysElapsed > 0 && daysElapsed <= daysInPeriod)
            {
                // Simple proportion: (days_elapsed / days_in_period) * interest_due
                proRataInterest = Math.Round(currentInstallment.InterestDue * ((decimal)daysElapsed / daysInPeriod), 2);
            }
            else if (daysElapsed > daysInPeriod)
            {
                proRataInterest = currentInstallment.InterestDue; // already overdue
            }
            
            proRataInterest -= currentInstallment.InterestPaid;
            if (proRataInterest < 0) proRataInterest = 0;
        }

        // Add interest from past overdue installments
        proRataInterest += unpaidSchedules.Where(s => s.DueDate < settlementDate)
                                          .Sum(s => s.InterestDue - s.InterestPaid);

        // 3. Early Settlement Fee
        var feeRate = loan.EarlySettlementFeeRate;
        var earlySettlementFee = Math.Round(remainingPrincipal * feeRate, 2);

        // 4. Late Fees
        var unpaidLateFees = loan.LateFees.Where(f => f.Status == LateFeeStatus.Pending).Sum(f => f.FeeAmount);

        var total = remainingPrincipal + proRataInterest + earlySettlementFee + unpaidLateFees;

        return Result.Success(new EarlySettlePreviewDto
        {
            SettlementDate = settlementDate,
            RemainingPrincipal = remainingPrincipal,
            ProRataInterest = proRataInterest,
            EarlySettlementFee = earlySettlementFee,
            UnpaidLateFees = unpaidLateFees,
            TotalSettlementAmount = total
        });
    }

    public async Task<Result> EarlySettleAsync(int loanId, EarlySettleRequest request, int userId)
    {
        try 
        {
            await EnsureSystemNotLockedAsync();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
        var previewResult = await PreviewEarlySettleAsync(loanId, request.SettlementDate);
        if (!previewResult.IsSuccess) return Result.Failure(previewResult.Error!);

        var preview = previewResult.Value!;
        var totalAmount = preview.TotalSettlementAmount;

        if (request.WaiveFee)
        {
            totalAmount -= preview.EarlySettlementFee;
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
        {
            fee.Status = LateFeeStatus.Paid;
        }

        // Mark all schedules as paid
        foreach (var s in loan.RepaymentSchedules.Where(s => s.Status != InstallmentStatus.Paid))
        {
            s.PrincipalPaid = s.PrincipalDue;
            s.InterestPaid = s.InterestDue; // Simplification: we mark full interest paid on DB level to close the schedule, but financially they only paid pro-rata.
                                            // In a perfect system, we would adjust the schedule's InterestDue to equal ProRata interest to avoid mismatch.
            if (s.DueDate >= request.SettlementDate)
            {
                s.InterestDue = s.InterestPaid = s == loan.RepaymentSchedules.FirstOrDefault(x => x.DueDate >= request.SettlementDate) 
                    ? preview.ProRataInterest 
                    : 0; // future installments have 0 interest because settled early
            }
            
            s.RemainingDueAmount = 0;
            s.Status = InstallmentStatus.Paid;
        }

        loan.OutstandingAmount = 0;
        loan.Status = LoanStatus.Closed;
        loan.UpdatedAt = _businessClock.UtcNow;

        var payment = new Payment
        {
            LoanId = loanId,
            PaymentDate = request.SettlementDate,
            Amount = totalAmount,
            PaymentMethod = "BankTransfer",
            Remarks = $"Early Settlement. Fee Waived: {request.WaiveFee}",
            CreatedByUserId = userId
        };
        _db.Payments.Add(payment);
        await SaveChangesWithConcurrencyHandlingAsync("Loan was modified while settling early. Reload and retry.");

        _db.LoanTransactions.Add(new LoanTransaction { LoanId = loanId, PaymentId = payment.Id, TransactionType = "EarlySettlement", Amount = totalAmount });
        if (preview.EarlySettlementFee > 0)
        {
            _db.LoanTransactions.Add(new LoanTransaction { LoanId = loanId, PaymentId = payment.Id, TransactionType = "EarlySettlementFee", Amount = preview.EarlySettlementFee });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.LoanEarlySettled,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = AuditHelper.ToJson(new { settlementAmount = totalAmount, feeWaived = request.WaiveFee })
        });
        
        // Post Accounting Journal Entry for Early Settlement
        var esCashCode = await _accountingService.GetAccountCodeAsync("Repayment_Principal", true);
        var esPrinCode = await _accountingService.GetAccountCodeAsync("Repayment_Principal", false);
        var esIntCode = await _accountingService.GetAccountCodeAsync("Repayment_Interest", false);
        var esFeeCode = await _accountingService.GetAccountCodeAsync("Repayment_LateFee", false);
        var esEarlyCode = await _accountingService.GetAccountCodeAsync("Repayment_EarlySettleFee", false);

        var journalLines = new List<JournalEntryLineDto>
        {
            new JournalEntryLineDto { AccountCode = esCashCode, DebitAmount = totalAmount, CreditAmount = 0, LineDescription = "Cash Inflow from Early Settle" }
        };

        if (preview.RemainingPrincipal > 0)
            journalLines.Add(new JournalEntryLineDto { AccountCode = esPrinCode, DebitAmount = 0, CreditAmount = preview.RemainingPrincipal, LineDescription = "Principal Repaid" });
        if (preview.ProRataInterest > 0)
            journalLines.Add(new JournalEntryLineDto { AccountCode = esIntCode, DebitAmount = 0, CreditAmount = preview.ProRataInterest, LineDescription = "Interest Repaid" });
        if (preview.UnpaidLateFees > 0)
            journalLines.Add(new JournalEntryLineDto { AccountCode = esFeeCode, DebitAmount = 0, CreditAmount = preview.UnpaidLateFees, LineDescription = "Late Fees Repaid" });
        if (preview.EarlySettlementFee > 0)
            journalLines.Add(new JournalEntryLineDto { AccountCode = esEarlyCode, DebitAmount = 0, CreditAmount = preview.EarlySettlementFee, LineDescription = "Early Settlement Fee Income" });

        var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
        {
            ReferenceNumber = $"EARLYSETTLE-{payment.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            BusinessDate = request.SettlementDate,
            Description = $"Early Settlement for Loan {loan.Id}",
            Lines = journalLines
        });

        if (!journalResult.IsSuccess)
        {
            await tx.RollbackAsync();
            throw new InvalidOperationException($"Accounting failed: {journalResult.Error}");
        }

        await SaveChangesWithConcurrencyHandlingAsync("Loan was modified while settling early. Reload and retry.");
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

    // ── GET /loans/{id}/schedule ─────────────────────────────────────────────
    public async Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId)
    {
        var today = _businessClock.TodayBusinessDate();
        return await _db.RepaymentSchedules
            .Where(s => s.LoanId == loanId)
            .OrderBy(s => s.InstallmentNo)
            .Select(s => MapSchedule(s, today))
            .ToListAsync();
    }

    // ── GET /loans/{id}/payments ─────────────────────────────────────────────
    public async Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int loanId)
    {
        return await _db.Payments
            .Include(p => p.CreatedBy)
            .Where(p => p.LoanId == loanId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => MapPayment(p))
            .ToListAsync();
    }

    // ── POST /loans/{id}/collection-notes (§OD-003) ──────────────────────────
    public async Task<Result> AddCollectionNoteAsync(int loanId, AddCollectionNoteRequest req, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure("Loan not found.");

        // COL-VAL-001
        if (string.IsNullOrWhiteSpace(req.NoteText))
            return Result.Failure("COL-VAL-001: Collection note cannot be empty.");

        _db.CollectionNotes.Add(new CollectionNote
        {
            LoanId = loanId,
            NoteDate = req.NoteDate,
            NoteText = req.NoteText,
            CreatedByUserId = userId
        });

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.CollectionNoteAdded,
            EntityType = "Loan",
            EntityId = loanId
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    // ── GET /overdue (§OD-002) — DB-side overdue projection ──────────────────
    public async Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync()
    {
        var today = _businessClock.TodayBusinessDate();

        // C1 Fix: DB-side projection — no full entity load
        var overdueData = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .Where(l => l.RepaymentSchedules.Any(s => s.DueDate < today && s.RemainingDueAmount > 0))
            .Select(l => new OverdueItemDto
            {
                LoanId = l.Id,
                LoanAccountNo = l.LoanAccountNo,
                CustomerId = l.CustomerId,
                CustomerName = l.Customer.FullName,
                CustomerPhone = l.Customer.Phone,
                OutstandingAmount = l.OutstandingAmount,
                OverdueDueAmount = l.RepaymentSchedules
                    .Where(s => s.DueDate < today && s.RemainingDueAmount > 0)
                    .Sum(s => s.RemainingDueAmount),
                Dpd = today.DayNumber - l.RepaymentSchedules
                    .Where(s => s.DueDate < today && s.RemainingDueAmount > 0)
                    .Min(s => s.DueDate).DayNumber,
                LastCollectionNote = l.CollectionNotes
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => n.NoteText)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.Dpd)
            .ToListAsync();

        // Set bucket on client side (simple string calc, not worth doing in SQL)
        foreach (var item in overdueData)
            item.Bucket = DpdBucket.Calculate(item.Dpd);

        return overdueData;
    }

    // ── Schedule generation (§13.2 — flat interest model) ────────────────────
    private void GenerateSchedule(Loan loan, DateOnly disbursementDate)
    {
        var principal = loan.ApprovedAmount;
        var rate = loan.MonthlyInterestRate;
        var n = loan.TenorMonths;

        var principalPerInstallment = Math.Round(principal / n, 2);
        var interestPerInstallment = Math.Round(principal * rate, 2);
        var lastPrincipal = principal - principalPerInstallment * (n - 1);

        for (int i = 1; i <= n; i++)
        {
            var p = i == n ? lastPrincipal : principalPerInstallment;
            var totalDue = p + interestPerInstallment;

            _db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = p,
                InterestDue = interestPerInstallment,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = totalDue,
                Status = InstallmentStatus.Unpaid
            });
        }
    }

    // ── Schedule generation (Reducing Balance / EMI) ────────────────────────
    private void GenerateReducingBalanceSchedule(Loan loan, DateOnly disbursementDate)
    {
        var principal = loan.ApprovedAmount;
        var rate = loan.MonthlyInterestRate;
        var n = loan.TenorMonths;
        var grace = loan.GracePeriodMonths;
        int repaymentMonths = n - grace;

        // EMI = P × r × (1+r)^n / ((1+r)^n - 1)
        decimal emi;
        if (rate == 0 || repaymentMonths <= 0)
        {
            emi = repaymentMonths > 0 ? Math.Round(principal / repaymentMonths, 2) : principal;
        }
        else
        {
            double r = (double)rate;
            double p = (double)principal;
            double nm = repaymentMonths;
            emi = Math.Round((decimal)(p * r * Math.Pow(1 + r, nm) / (Math.Pow(1 + r, nm) - 1)), 2);
        }

        decimal remaining = principal;

        for (int i = 1; i <= n; i++)
        {
            // Promotional rate for first N months
            var effectiveRate = (loan.PromotionalRate.HasValue && i <= loan.PromotionalMonths)
                ? loan.PromotionalRate.Value
                : rate;

            var interestDue = Math.Round(remaining * effectiveRate, 2);
            decimal principalDue;

            if (i <= grace)
            {
                // Grace period: interest only, no principal
                principalDue = 0;
            }
            else if (i == n)
            {
                // Last installment: pay all remaining principal
                principalDue = remaining;
            }
            else
            {
                principalDue = Math.Round(emi - interestDue, 2);
                if (principalDue > remaining) principalDue = remaining;
                if (principalDue < 0) principalDue = 0;
            }

            remaining -= principalDue;
            if (remaining < 0) remaining = 0;

            _db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = principalDue,
                InterestDue = interestDue,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = principalDue + interestDue,
                Status = InstallmentStatus.Unpaid
            });
        }
    }

    // ── Loan account number generator (§LOAN-001: unique) ────────────────────
    private async Task<string> GenerateLoanAccountNoAsync()
    {
        var year = _businessClock.LocalNow.Year;
        var prefix = $"LN-{year}-";
        var substringStart = prefix.Length + 1;
        var likePattern = $"{prefix}%";

        // Use raw SQL with locking to prevent race condition on concurrent CreateLoan
        // Parameters are passed via {0}, {1} — EF Core parameterizes them safely.
        var maxSeq = await _db.Database
            .SqlQueryRaw<int?>(
                "SELECT MAX(CAST(SUBSTRING(LoanAccountNo, {0}) AS UNSIGNED)) AS Value FROM Loans WHERE LoanAccountNo LIKE {1} FOR UPDATE",
                substringStart,
                likePattern)
            .FirstOrDefaultAsync();

        int seq = (maxSeq ?? 0) + 1;
        return $"{prefix}{seq:D4}";
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────
    private static LoanDto MapToDto(Loan l) => new()
    {
        Id = l.Id, LoanAccountNo = l.LoanAccountNo,
        ApplicationId = l.ApplicationId,
        CustomerId = l.CustomerId, CustomerName = l.Customer?.FullName ?? "",
        ProductName = l.Product?.ProductName ?? "",
        ApprovedAmount = l.ApprovedAmount,
        TenorMonths = l.TenorMonths,
        MonthlyInterestRate = l.MonthlyInterestRate,
        ProcessingFee = l.ProcessingFee,
        DisbursementDate = l.DisbursementDate,
        MaturityDate = l.MaturityDate,
        Status = l.Status,
        OutstandingAmount = l.OutstandingAmount,
        VirtualAccountNo = l.VirtualAccountNo,
        CreatedAt = l.CreatedAt
    };

    private static ScheduleItemDto MapSchedule(RepaymentSchedule s, DateOnly today) => new()
    {
        InstallmentNo = s.InstallmentNo,
        DueDate = s.DueDate,
        PrincipalDue = s.PrincipalDue,
        InterestDue = s.InterestDue,
        TotalDue = s.PrincipalDue + s.InterestDue,
        PrincipalPaid = s.PrincipalPaid,
        InterestPaid = s.InterestPaid,
        RemainingDueAmount = s.RemainingDueAmount,
        Status = s.Status,
        Dpd = s.DueDate < today && s.RemainingDueAmount > 0
            ? today.DayNumber - s.DueDate.DayNumber
            : 0
    };

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PaymentDate = p.PaymentDate,
        Amount = p.Amount,
        PaymentMethod = p.PaymentMethod,
        Remarks = p.Remarks,
        CreatedBy = p.CreatedBy?.FullName ?? "",
        CreatedAt = p.CreatedAt
    };

    private async Task SaveChangesWithConcurrencyHandlingAsync(string message)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(message);
        }
    }
}
