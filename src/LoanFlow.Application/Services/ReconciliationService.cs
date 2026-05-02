using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessCalendarService _businessCalendar;
    private readonly IBusinessClock _businessClock;

    public ReconciliationService(LoanFlowDbContext db, IBusinessCalendarService businessCalendar, IBusinessClock businessClock)
    {
        _db = db;
        _businessCalendar = businessCalendar;
        _businessClock = businessClock;
    }

    public async Task<ReconciliationReportDto> RunReconciliationAsync(DateOnly businessDate, int? userId)
    {
        var report = new ReconciliationReportDto { BusinessDate = businessDate };
        var (startUtc, endUtc) = _businessCalendar.GetBusinessDayUtcRange(businessDate);

        var existingOpenExceptions = await _db.ReconciliationExceptions
            .Where(e => e.BusinessDate == businessDate && e.Status == ReconciliationStatus.Open)
            .ToListAsync();

        var paymentsOnDate = await _db.Payments
            .Where(p => p.PaymentDate == businessDate)
            .Select(p => new { p.Id, p.LoanId, p.Amount })
            .ToListAsync();
        var positivePayments = paymentsOnDate.Where(p => p.Amount > 0).ToList();
        var positivePaymentIds = positivePayments.Select(p => p.Id).ToList();

        var totalPayments = positivePayments.Sum(p => p.Amount);
        var totalPaymentTx = await _db.LoanTransactions
            .Where(t => t.TransactionType == "Payment" && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .SumAsync(t => t.Amount);
        var totalPaymentAllocations = positivePaymentIds.Count == 0
            ? 0m
            : await _db.PaymentAllocations
                .Where(a => positivePaymentIds.Contains(a.PaymentId))
                .SumAsync(a => a.Amount);

        report.TotalPaymentsAmount = totalPayments;
        report.TotalPaymentTransactionsAmount = totalPaymentTx;
        report.TotalPaymentAllocationsAmount = totalPaymentAllocations;

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.PaymentMismatch,
            totalPayments,
            totalPaymentTx,
            $"Payment total ({totalPayments:N2}) does not match payment transaction total ({totalPaymentTx:N2}).");

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.AllocationMismatch,
            totalPayments,
            totalPaymentAllocations,
            $"Payment total ({totalPayments:N2}) does not match chained allocation total ({totalPaymentAllocations:N2}).");

        var totalDisb = await _db.LoanDisbursements
            .Where(d => d.DisbursementDate == businessDate)
            .SumAsync(d => d.DisbursedAmount);
        var totalDisbTx = await _db.LoanTransactions
            .Where(t => t.TransactionType == "Disbursement" && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .SumAsync(t => t.Amount);

        report.TotalDisbursementsAmount = totalDisb;
        report.TotalDisbursementTransactionsAmount = totalDisbTx;

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.DisbursementMismatch,
            totalDisb,
            totalDisbTx,
            $"Disbursement total ({totalDisb:N2}) does not match disbursement transaction total ({totalDisbTx:N2}).");

        var totalReversals = await _db.TransactionReversals
            .Where(r => r.Status == ReversalStatus.Approved
                && r.ApprovedAt.HasValue
                && r.ApprovedAt >= startUtc
                && r.ApprovedAt < endUtc)
            .SumAsync(r => r.ReversedAmount);
        var totalReversalTx = await _db.LoanTransactions
            .Where(t => t.TransactionType == "PaymentReversal" && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .SumAsync(t => t.Amount < 0 ? -t.Amount : t.Amount);

        report.TotalReversalsAmount = totalReversals;
        report.TotalReversalTransactionsAmount = totalReversalTx;

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.ReversalMismatch,
            totalReversals,
            totalReversalTx,
            $"Approved reversal total ({totalReversals:N2}) does not match compensating reversal transaction total ({totalReversalTx:N2}).");

        var totalLateFees = await _db.LateFees
            .Where(f => f.CreatedAt >= startUtc && f.CreatedAt < endUtc)
            .SumAsync(f => f.FeeAmount);
        var totalLateFeeTx = await _db.LoanTransactions
            .Where(t => t.TransactionType == "LateFeeAccrual" && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .SumAsync(t => t.Amount);

        report.TotalLateFeesAmount = totalLateFees;
        report.TotalLateFeeTransactionsAmount = totalLateFeeTx;

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.LateFeeMismatch,
            totalLateFees,
            totalLateFeeTx,
            $"Late fee accrual total ({totalLateFees:N2}) does not match late fee transaction total ({totalLateFeeTx:N2}).");

        var totalSuspenseAllocations = positivePaymentIds.Count == 0
            ? 0m
            : await _db.PaymentAllocations
                .Where(a => positivePaymentIds.Contains(a.PaymentId) && a.AllocationType == "Suspense")
                .SumAsync(a => a.Amount);
        var totalSuspenseTransactions = positivePaymentIds.Count == 0
            ? 0m
            : await _db.SuspenseTransactions
                .Where(s => s.SourcePaymentId.HasValue && positivePaymentIds.Contains(s.SourcePaymentId.Value))
                .SumAsync(s => s.Amount);

        report.TotalSuspenseAllocationsAmount = totalSuspenseAllocations;
        report.TotalSuspenseTransactionsAmount = totalSuspenseTransactions;

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.SuspenseMismatch,
            totalSuspenseAllocations,
            totalSuspenseTransactions,
            $"Suspense allocation total ({totalSuspenseAllocations:N2}) does not match suspense transaction total ({totalSuspenseTransactions:N2}).");

        var journalLinesForDay = await _db.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.BusinessDate == businessDate)
            .ToListAsync();

        var totalDr = journalLinesForDay.Sum(l => l.DebitAmount);
        var totalCr = journalLinesForDay.Sum(l => l.CreditAmount);

        ReconcileAggregate(
            report,
            existingOpenExceptions,
            businessDate,
            userId,
            ReconciliationExceptionType.TrialBalanceMismatch,
            totalDr,
            totalCr,
            $"Trial Balance mismatch for the day: Total Debits ({totalDr:N2}) does not match Total Credits ({totalCr:N2}).");

        var activeLoans = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .ToListAsync();

        var activeLoanIds = activeLoans.Select(l => l.Id).ToList();
        var pendingSuspenseByLoan = await _db.SuspenseTransactions
            .Where(s => activeLoanIds.Contains(s.LoanId) && s.Status == SuspenseStatus.Pending)
            .GroupBy(s => s.LoanId)
            .Select(g => new { LoanId = g.Key, TotalAmount = g.Sum(s => s.Amount) })
            .ToDictionaryAsync(k => k.LoanId, v => v.TotalAmount);

        foreach (var loan in activeLoans)
        {
            var computedOutstanding = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.RemainingDueAmount);
            var pendingLateFees = loan.LateFees
                .Where(f => f.Status == LateFeeStatus.Pending)
                .Sum(f => f.FeeAmount);
            
            var pendingSuspense = pendingSuspenseByLoan.TryGetValue(loan.Id, out var amount) ? amount : 0m;

            report.TotalOutstandingComputedAmount += computedOutstanding;
            report.TotalOutstandingStoredAmount += loan.OutstandingAmount;
            report.TotalPendingLateFeesAmount += pendingLateFees;
            report.TotalPendingSuspenseAmount += pendingSuspense;

            if (!AmountsMatch(loan.OutstandingAmount, computedOutstanding))
            {
                var exc = UpsertOpenException(
                    existingOpenExceptions,
                    businessDate,
                    ReconciliationExceptionType.OutstandingMismatch,
                    computedOutstanding,
                    loan.OutstandingAmount,
                    $"Loan {loan.LoanAccountNo}: stored outstanding ({loan.OutstandingAmount:N2}) != computed principal+interest outstanding ({computedOutstanding:N2}); pending late fees = {pendingLateFees:N2}; pending suspense = {pendingSuspense:N2}.",
                    loan.Id);
                report.Exceptions.Add(MapExceptionToDto(exc));
            }
            else
            {
                CloseOpenExceptions(
                    existingOpenExceptions,
                    businessDate,
                    ReconciliationExceptionType.OutstandingMismatch,
                    userId,
                    "Mismatch resolved by reconciliation rerun.",
                    loan.Id);
            }
        }

        report.ExceptionsFound = report.Exceptions.Count;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.ReconciliationRun,
            EntityType = "Reconciliation",
            EntityId = 0,
            NewValuesJson = AuditHelper.ToJson(new { businessDate = businessDate.ToString(), exceptions = report.ExceptionsFound })
        });

        await _db.SaveChangesAsync();
        return report;
    }

    private void ReconcileAggregate(
        ReconciliationReportDto report,
        IEnumerable<ReconciliationException> existingOpenExceptions,
        DateOnly businessDate,
        int? userId,
        string exceptionType,
        decimal expectedAmount,
        decimal actualAmount,
        string description)
    {
        if (!AmountsMatch(expectedAmount, actualAmount))
        {
            var exc = UpsertOpenException(
                existingOpenExceptions,
                businessDate,
                exceptionType,
                expectedAmount,
                actualAmount,
                description);
            report.Exceptions.Add(MapExceptionToDto(exc));
            return;
        }

        CloseOpenExceptions(
            existingOpenExceptions,
            businessDate,
            exceptionType,
            userId,
            "Mismatch resolved by reconciliation rerun.");
    }

    private ReconciliationException UpsertOpenException(
        IEnumerable<ReconciliationException> existingOpenExceptions,
        DateOnly businessDate,
        string exceptionType,
        decimal expectedAmount,
        decimal actualAmount,
        string description,
        int? loanId = null)
    {
        var exc = existingOpenExceptions.FirstOrDefault(e =>
            e.BusinessDate == businessDate
            && e.ExceptionType == exceptionType
            && e.Status == ReconciliationStatus.Open
            && e.LoanId == loanId);

        if (exc == null)
        {
            exc = new ReconciliationException
            {
                BusinessDate = businessDate,
                ExceptionType = exceptionType,
                LoanId = loanId,
                Status = ReconciliationStatus.Open
            };
            _db.ReconciliationExceptions.Add(exc);
        }

        exc.ExpectedAmount = expectedAmount;
        exc.ActualAmount = actualAmount;
        exc.Difference = expectedAmount - actualAmount;
        exc.Description = description;
        exc.ResolvedByUserId = null;
        exc.ResolvedAt = null;
        exc.Resolution = null;

        return exc;
    }

    private void CloseOpenExceptions(
        IEnumerable<ReconciliationException> existingOpenExceptions,
        DateOnly businessDate,
        string exceptionType,
        int? userId,
        string resolution,
        int? loanId = null)
    {
        var exceptionsToClose = existingOpenExceptions
            .Where(e => e.BusinessDate == businessDate
                && e.ExceptionType == exceptionType
                && e.Status == ReconciliationStatus.Open
                && e.LoanId == loanId)
            .ToList();

        foreach (var exc in exceptionsToClose)
        {
            exc.Status = ReconciliationStatus.Resolved;
            exc.ResolvedByUserId = userId;
            exc.ResolvedAt = _businessClock.UtcNow;
            exc.Resolution = resolution;
        }
    }

    public async Task<PagedResult<ReconciliationExceptionDto>> GetExceptionsAsync(string? status, int page = 1, int pageSize = 50)
    {
        var query = _db.ReconciliationExceptions
            .Include(e => e.Loan)
            .Include(e => e.ResolvedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ReconciliationExceptionDto>
        {
            Items = items.Select(MapExceptionToDto),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result> ResolveExceptionAsync(int exceptionId, ResolveExceptionDto request, int userId)
    {
        var exc = await _db.ReconciliationExceptions.FindAsync(exceptionId);
        if (exc is null) return Result.Failure("Exception not found.");
        if (exc.Status != ReconciliationStatus.Open)
            return Result.Failure("Only open exceptions can be resolved.");
        if (string.IsNullOrWhiteSpace(request.Resolution))
            return Result.Failure("Resolution description is required.");

        exc.Status = ReconciliationStatus.Resolved;
        exc.ResolvedByUserId = userId;
        exc.ResolvedAt = _businessClock.UtcNow;
        exc.Resolution = request.Resolution;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.ReconciliationExceptionResolved,
            EntityType = "ReconciliationException",
            EntityId = exceptionId,
            NewValuesJson = AuditHelper.ToJson(new { resolution = request.Resolution })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private static bool AmountsMatch(decimal expectedAmount, decimal actualAmount) =>
        Math.Abs(expectedAmount - actualAmount) <= 0.01m;

    private static ReconciliationExceptionDto MapExceptionToDto(ReconciliationException e) => new()
    {
        Id = e.Id,
        BusinessDate = e.BusinessDate,
        ExceptionType = e.ExceptionType,
        LoanId = e.LoanId,
        LoanAccountNo = e.Loan?.LoanAccountNo,
        ExpectedAmount = e.ExpectedAmount,
        ActualAmount = e.ActualAmount,
        Difference = e.Difference,
        Description = e.Description,
        Status = e.Status,
        ResolvedBy = e.ResolvedBy?.FullName,
        ResolvedAt = e.ResolvedAt,
        Resolution = e.Resolution
    };
}
