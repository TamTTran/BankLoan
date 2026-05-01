using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── Interface ────────────────────────────────────────────────────────────────

public interface ILoanDisbursementService
{
    Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId);
    Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest request, int userId);
}

// ── Implementation ───────────────────────────────────────────────────────────

/// <summary>
/// Tất cả operations tạo và giải ngân khoản vay.
/// Tách ra từ LoanService để giảm god-class 56KB xuống còn ~20KB mỗi service.
/// </summary>
public class LoanDisbursementService : ILoanDisbursementService
{
    private readonly LoanFlowDbContext       _db;
    private readonly IBusinessClock          _businessClock;
    private readonly IAccountingService      _accountingService;
    private readonly IPaymentGatewayService  _paymentGatewayService;
    private readonly ILoanQueryService       _queryService;

    public LoanDisbursementService(
        LoanFlowDbContext db,
        IBusinessClock businessClock,
        IAccountingService accountingService,
        IPaymentGatewayService paymentGatewayService,
        ILoanQueryService queryService)
    {
        _db                    = db;
        _businessClock         = businessClock;
        _accountingService     = accountingService;
        _paymentGatewayService = paymentGatewayService;
        _queryService          = queryService;
    }

    // ── POST /applications/{id}/create-loan ──────────────────────────────────

    public async Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId)
    {
        var application = await _db.LoanApplications
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null) return Result.Failure<LoanDto>("LOAN-VAL-001: Application not found.");
        if (application.Status != ApplicationStatus.Approved)
            return Result.Failure<LoanDto>("LOAN-VAL-001: Only approved application can create loan.");
        if (await _db.Loans.AnyAsync(l => l.ApplicationId == applicationId))
            return Result.Failure<LoanDto>("LOAN-VAL-002: Loan already created for this application.");

        var product = application.Product;
        var loanNo  = await GenerateLoanAccountNoAsync();

        var loan = new Loan
        {
            LoanAccountNo        = loanNo,
            ApplicationId        = applicationId,
            CustomerId           = application.CustomerId,
            ProductId            = application.ProductId,
            ApprovedAmount       = application.RequestedAmount,
            TenorMonths          = application.RequestedTenorMonths,
            MonthlyInterestRate  = product.MonthlyInterestRate,
            ProcessingFee        = product.ProcessingFee,
            Status               = LoanStatus.PendingDisbursement,
            OutstandingAmount    = application.RequestedAmount,
            InterestMethod       = product.InterestMethod,
            GracePeriodMonths    = product.GracePeriodMonths,
            PromotionalRate      = product.PromotionalRate,
            PromotionalMonths    = product.PromotionalMonths,
            EarlySettlementFeeRate = product.EarlySettlementFeeRate,
            LateFeeRate          = product.LateFeeRate,
            LateFeeGraceDays     = product.LateFeeGraceDays,
            ProductType          = product.ProductType
        };
        _db.Loans.Add(loan);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId   = userId,
            ActionCode    = AuditActions.LoanCreated,
            EntityType    = "Loan",
            EntityId      = 0,
            NewValuesJson = AuditHelper.ToJson(new { loanAccountNo = loanNo, applicationId })
        });

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.SaveChangesAsync();
            loan.VirtualAccountNo = $"VA{loan.Id:D8}";
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }

        return await _queryService.GetByIdAsync(loan.Id);
    }

    // ── POST /loans/{id}/disburse ─────────────────────────────────────────────

    public async Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest req, int userId)
    {
        // EOD lock check
        var eodRunning = await _db.SystemConfigs.FirstOrDefaultAsync(s => s.Key == "IsEodRunning");
        if (eodRunning?.Value == "true")
            return Result.Failure<LoanDto>("Hệ thống đang chạy Back-end cuối ngày (EOD). Không thể xử lý giao dịch lúc này.");

        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.LoanCollaterals)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure<LoanDto>("Loan not found.");
        if (loan.Status != LoanStatus.PendingDisbursement)
            return Result.Failure<LoanDto>("DISB-VAL-001: Loan is not ready for disbursement.");
        if (await _db.LoanDisbursements.AnyAsync(d => d.LoanId == loanId))
            return Result.Failure<LoanDto>("DISB-VAL-003: Loan already disbursed.");
        if (!await _db.EContracts.AnyAsync(e => e.LoanId == loanId))
            return Result.Failure<LoanDto>("APP-VAL-006: E-Contract is not signed. Digital signature is required before disbursement.");

        // LTV validation
        if (loan.Product != null && loan.Product.RequiresCollateral)
        {
            if (!loan.Product.MaxLtvRatio.HasValue || loan.Product.MaxLtvRatio.Value <= 0)
                return Result.Failure<LoanDto>("Product requires collateral but Max LTV Ratio is not configured correctly (> 0).");
            var sumAllocated      = loan.LoanCollaterals.Sum(lc => lc.AllocatedValue);
            var reqCollateralValue = loan.ApprovedAmount / (loan.Product.MaxLtvRatio.Value / 100m);
            if (sumAllocated < reqCollateralValue)
                return Result.Failure<LoanDto>($"LTV Validation Failed: Required {reqCollateralValue:N0} but only {sumAllocated:N0} allocated.");
        }

        if (req.DisbursedAmount != loan.ApprovedAmount)
            return Result.Failure<LoanDto>($"DISB-VAL-002: Disbursed amount must equal approved amount ({loan.ApprovedAmount:N0}).");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            loan.DisbursementDate = req.DisbursementDate;
            loan.MaturityDate     = req.DisbursementDate.AddMonths(loan.TenorMonths);
            loan.Status           = LoanStatus.Active;
            loan.UpdatedAt        = _businessClock.UtcNow;

            var disbursement = new LoanDisbursement
            {
                LoanId            = loanId,
                DisbursedAmount   = req.DisbursedAmount,
                DisbursementDate  = req.DisbursementDate,
                RecordedByUserId  = userId,
                DisbursementMethod = "Cash",
                CreatedAt         = _businessClock.UtcNow
            };
            _db.LoanDisbursements.Add(disbursement);

            var loanTx = new LoanTransaction
            {
                LoanId = loanId, TransactionType = "Disbursement",
                Amount = req.DisbursedAmount, CreatedAt = _businessClock.UtcNow
            };
            _db.LoanTransactions.Add(loanTx);

            // Generate repayment schedule
            if (loan.InterestMethod == InterestMethod.ReducingBalance)
                ScheduleGenerator.GenerateReducingBalance(_db, loan, req.DisbursementDate);
            else
                ScheduleGenerator.GenerateFlat(_db, loan, req.DisbursementDate);

            // Accounting journal
            var debitCode  = await _accountingService.GetAccountCodeAsync("Disbursement", true);
            var creditCode = await _accountingService.GetAccountCodeAsync("Disbursement", false);
            var journalLines = new List<JournalEntryLineDto>
            {
                new() { AccountCode = debitCode, DebitAmount = req.DisbursedAmount, CreditAmount = 0, LineDescription = "Loan Principal Disbursement" }
            };
            if (loan.ProcessingFee > 0)
            {
                var feeCode = await _accountingService.GetAccountCodeAsync("ProcessingFee", false);
                journalLines.Add(new() { AccountCode = creditCode, DebitAmount = 0, CreditAmount = req.DisbursedAmount - loan.ProcessingFee, LineDescription = "Net Cash Outflow" });
                journalLines.Add(new() { AccountCode = feeCode, DebitAmount = 0, CreditAmount = loan.ProcessingFee, LineDescription = "Processing Fee Deduced Upfront" });
            }
            else journalLines.Add(new() { AccountCode = creditCode, DebitAmount = 0, CreditAmount = req.DisbursedAmount, LineDescription = "Cash Outflow" });

            var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
            {
                ReferenceNumber = $"DISB-{loanId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                BusinessDate    = req.DisbursementDate,
                Description     = $"Disbursement for Loan {loan.LoanAccountNo}",
                Lines           = journalLines
            });
            if (!journalResult.IsSuccess) { await tx.RollbackAsync(); return Result.Failure<LoanDto>($"Accounting failed: {journalResult.Error}"); }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId, ActionCode = AuditActions.LoanDisbursed, EntityType = "Loan", EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new { disbursedAmount = req.DisbursedAmount, disbursementDate = req.DisbursementDate })
            });

            // NAPAS auto-disbursement
            var isAutoDisburse = !string.IsNullOrWhiteSpace(loan.Customer.BankAccountNumber)
                               && !string.IsNullOrWhiteSpace(loan.Customer.BankCode);
            if (isAutoDisburse)
            {
                disbursement.DisbursementMethod = "BankTransfer";
                disbursement.DisbursementStatus = DisbursementStatus.Pending;
            }

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified while disbursing."); }

            await tx.CommitAsync();

            // NAPAS call AFTER commit
            if (isAutoDisburse)
            {
                var gw = await _paymentGatewayService.TransferToCustomerAsync(
                    loan.Customer.BankCode!, loan.Customer.BankAccountNumber!,
                    req.DisbursedAmount, $"Disbursement for Loan {loan.LoanAccountNo}");

                if (!gw.IsSuccess)
                {
                    disbursement.DisbursementStatus  = DisbursementStatus.Failed;
                    disbursement.GatewayErrorMessage = gw.Error;
                    disbursement.GatewayConfirmedAt  = _businessClock.UtcNow;
                    _db.TransactionReversals.Add(new TransactionReversal
                    {
                        LoanId = loanId, OriginalTransactionId = loanTx.Id, ReversalType = "DisbursementCorrection",
                        OriginalAmount = req.DisbursedAmount, ReversedAmount = 0,
                        Reason = $"NAPAS Auto-Disbursement Failed: {gw.Error}", Status = "Pending", RequestedByUserId = userId
                    });
                    _db.AuditLogs.Add(new AuditLog { ActorUserId = userId, ActionCode = AuditActions.AutoDisbursementFailed, EntityType = "LoanDisbursement", EntityId = disbursement.Id, NewValuesJson = AuditHelper.ToJson(new { error = gw.Error }) });
                    await _db.SaveChangesAsync();
                    return Result.Failure<LoanDto>($"NAPAS Auto-Disbursement Failed: {gw.Error}. Pending reversal created for Approver.");
                }

                var gwVal = gw.Value;
                disbursement.NapasReferenceId   = gwVal.ReferenceId;
                disbursement.NapasTransactionId = gwVal.IsConfirmedImmediately ? gwVal.TransactionId : null;
                disbursement.DisbursementStatus = gwVal.IsConfirmedImmediately ? DisbursementStatus.Completed : DisbursementStatus.Pending;
                if (gwVal.IsConfirmedImmediately) disbursement.GatewayConfirmedAt = gwVal.ProcessedAt;
                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = userId, ActionCode = gwVal.IsConfirmedImmediately ? AuditActions.AutoDisbursementSuccess : AuditActions.AutoDisbursementPending,
                    EntityType = "LoanDisbursement", EntityId = disbursement.Id,
                    NewValuesJson = AuditHelper.ToJson(new { gwVal.ReferenceId, gwVal.TransactionId, gwVal.ProcessedAt })
                });
                await _db.SaveChangesAsync();
            }

            return await _queryService.GetByIdAsync(loanId);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); throw new ConcurrencyConflictException("Loan was modified while disbursing."); }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GenerateLoanAccountNoAsync()
    {
        var year    = _businessClock.LocalNow.Year;
        var prefix  = $"LN-{year}-";

        // LINQ-based — compatible with both InMemory (tests) and MySQL (production).
        // For high-concurrency prod, consider a DB sequence or row-locking stored proc.
        var existing = await _db.Loans
            .Where(l => l.LoanAccountNo.StartsWith(prefix))
            .Select(l => l.LoanAccountNo)
            .ToListAsync();

        var maxSeq = existing.Count == 0
            ? 0
            : existing
                .Select(no =>
                {
                    var suffix = no[prefix.Length..];
                    return int.TryParse(suffix, out var n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

        return $"{prefix}{maxSeq + 1:D4}";
    }
}
