using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class ReconciliationReportDto
{
    public DateOnly BusinessDate { get; set; }
    public decimal TotalPaymentsAmount { get; set; }
    public decimal TotalPaymentTransactionsAmount { get; set; }
    public decimal TotalDisbursementsAmount { get; set; }
    public decimal TotalDisbursementTransactionsAmount { get; set; }
    public int ExceptionsFound { get; set; }
    public List<ReconciliationExceptionDto> Exceptions { get; set; } = [];
}

public class ReconciliationExceptionDto
{
    public int Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string ExceptionType { get; set; } = "";
    public int? LoanId { get; set; }
    public string? LoanAccountNo { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Difference { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

public class ResolveExceptionDto
{
    public string Resolution { get; set; } = "";
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IReconciliationService
{
    Task<ReconciliationReportDto> RunReconciliationAsync(DateOnly businessDate, int? userId);
    Task<PagedResult<ReconciliationExceptionDto>> GetExceptionsAsync(string? status, int page = 1, int pageSize = 50);
    Task<Result> ResolveExceptionAsync(int exceptionId, ResolveExceptionDto request, int userId);
}

// ── Implementation ──────────────────────────────────────────────────────────
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

        // ── 1. Payment reconciliation ────────────────────────────────────
        // Sum payments on date
        var totalPayments = await _db.Payments
            .Where(p => p.PaymentDate == businessDate && p.Amount > 0) // exclude compensating (negative) payments
            .SumAsync(p => p.Amount);

        // Sum payment transactions on date
        var totalPaymentTx = await _db.LoanTransactions
            .Where(t => t.TransactionType == "Payment" && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .SumAsync(t => t.Amount);

        report.TotalPaymentsAmount = totalPayments;
        report.TotalPaymentTransactionsAmount = totalPaymentTx;

        if (totalPayments != totalPaymentTx)
        {
            var exc = existingOpenExceptions.FirstOrDefault(e => e.ExceptionType == ReconciliationExceptionType.PaymentMismatch);
            if (exc == null)
            {
                exc = new ReconciliationException
                {
                    BusinessDate = businessDate,
                    ExceptionType = ReconciliationExceptionType.PaymentMismatch,
                    Status = ReconciliationStatus.Open
                };
                _db.ReconciliationExceptions.Add(exc);
            }
            
            exc.ExpectedAmount = totalPayments;
            exc.ActualAmount = totalPaymentTx;
            exc.Difference = totalPayments - totalPaymentTx;
            exc.Description = $"Payment total ({totalPayments:N2}) does not match transaction total ({totalPaymentTx:N2}).";
            
            report.Exceptions.Add(MapExceptionToDto(exc));
        }

        // ── 2. Disbursement reconciliation ───────────────────────────────
        var totalDisb = await _db.LoanDisbursements
            .Where(d => d.DisbursementDate == businessDate)
            .SumAsync(d => d.DisbursedAmount);

        var totalDisbTx = await _db.LoanTransactions
            .Where(t => t.TransactionType == "Disbursement" && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .SumAsync(t => t.Amount);

        report.TotalDisbursementsAmount = totalDisb;
        report.TotalDisbursementTransactionsAmount = totalDisbTx;

        if (totalDisb != totalDisbTx)
        {
            var exc = existingOpenExceptions.FirstOrDefault(e => e.ExceptionType == ReconciliationExceptionType.DisbursementMismatch);
            if (exc == null)
            {
                exc = new ReconciliationException
                {
                    BusinessDate = businessDate,
                    ExceptionType = ReconciliationExceptionType.DisbursementMismatch,
                    Status = ReconciliationStatus.Open
                };
                _db.ReconciliationExceptions.Add(exc);
            }
            
            exc.ExpectedAmount = totalDisb;
            exc.ActualAmount = totalDisbTx;
            exc.Difference = totalDisb - totalDisbTx;
            exc.Description = $"Disbursement total ({totalDisb:N2}) does not match transaction total ({totalDisbTx:N2}).";
            
            report.Exceptions.Add(MapExceptionToDto(exc));
        }

        // ── 3. Outstanding balance verification ─────────────────────────
        var activeLoans = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .ToListAsync();

        foreach (var loan in activeLoans)
        {
            var computedOutstanding = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.RemainingDueAmount);

            if (Math.Abs(loan.OutstandingAmount - computedOutstanding) > 0.01m)
            {
                var exc = existingOpenExceptions.FirstOrDefault(e => 
                    e.ExceptionType == ReconciliationExceptionType.OutstandingMismatch && e.LoanId == loan.Id);
                
                if (exc == null)
                {
                    exc = new ReconciliationException
                    {
                        BusinessDate = businessDate,
                        ExceptionType = ReconciliationExceptionType.OutstandingMismatch,
                        LoanId = loan.Id,
                        Status = ReconciliationStatus.Open
                    };
                    _db.ReconciliationExceptions.Add(exc);
                }
                
                exc.ExpectedAmount = computedOutstanding;
                exc.ActualAmount = loan.OutstandingAmount;
                exc.Difference = loan.OutstandingAmount - computedOutstanding;
                exc.Description = $"Loan {loan.LoanAccountNo}: stored outstanding ({loan.OutstandingAmount:N2}) != computed ({computedOutstanding:N2}).";
                
                report.Exceptions.Add(MapExceptionToDto(exc));
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
