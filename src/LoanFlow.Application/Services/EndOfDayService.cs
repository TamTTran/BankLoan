using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class EodReportDto
{
    public DateOnly BusinessDate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    // Step results
    public OverdueJobResultDto? OverdueResult { get; set; }
    public ReconciliationReportDto? ReconciliationResult { get; set; }

    // Portfolio summary
    public int TotalActiveLoans { get; set; }
    public int TotalOverdueLoans { get; set; }
    public int TotalClosedLoans { get; set; }
    public decimal TotalDisbursedToday { get; set; }
    public decimal TotalPaymentsToday { get; set; }
    public int Errors { get; set; }
    public int BrokenPromises { get; set; }
    public List<string> ErrorMessages { get; set; } = [];
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IEndOfDayService
{
    Task<EodReportDto> RunAsync(DateOnly businessDate, int? userId = null, CancellationToken ct = default);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class EndOfDayService : IEndOfDayService
{
    private readonly IOverdueJobService _overdueService;
    private readonly IReconciliationService _reconciliationService;
    private readonly ICollectionService _collectionService;
    private readonly INotificationService _notificationService;
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<EndOfDayService> _logger;

    public EndOfDayService(
        IOverdueJobService overdueService,
        IReconciliationService reconciliationService,
        ICollectionService collectionService,
        INotificationService notificationService,
        LoanFlowDbContext db,
        ILogger<EndOfDayService> logger)
    {
        _overdueService = overdueService;
        _reconciliationService = reconciliationService;
        _collectionService = collectionService;
        _notificationService = notificationService;
        _db = db;
        _logger = logger;
    }

    public async Task<EodReportDto> RunAsync(DateOnly businessDate, int? userId = null, CancellationToken ct = default)
    {
        var report = new EodReportDto
        {
            BusinessDate = businessDate,
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("EOD processing started for {Date}", businessDate);

        // ── Step 1: Overdue Aging + Late Fee Generation + Bucket Snapshots ──
        try
        {
            report.OverdueResult = await _overdueService.RunDailyOverdueProcessingAsync(businessDate, ct: ct);
            _logger.LogInformation("EOD Step 1 (Overdue) completed: {Overdue} marked, {Reverted} reverted",
                report.OverdueResult.LoansMarkedOverdue, report.OverdueResult.LoansRevertedToActive);
        }
        catch (Exception ex)
        {
            report.Errors++;
            report.ErrorMessages.Add($"Step 1 (Overdue): {ex.Message}");
            _logger.LogError(ex, "EOD Step 1 failed");
        }

        // ── Step 1.5: Check Broken Promises ────────────────────────────────
        try
        {
            report.BrokenPromises = await _collectionService.CheckBrokenPromisesAsync(businessDate);
            _logger.LogInformation("EOD Step 1.5 (Broken Promises) completed: {Broken} broken",
                report.BrokenPromises);
        }
        catch (Exception ex)
        {
            report.Errors++;
            report.ErrorMessages.Add($"Step 1.5 (Broken Promises): {ex.Message}");
            _logger.LogError(ex, "EOD Step 1.5 failed");
        }

        // ── Step 2: Reconciliation ──────────────────────────────────────────
        try
        {
            report.ReconciliationResult = await _reconciliationService.RunReconciliationAsync(businessDate, userId);
            _logger.LogInformation("EOD Step 2 (Reconciliation) completed: {Exceptions} exceptions",
                report.ReconciliationResult.ExceptionsFound);
        }
        catch (Exception ex)
        {
            report.Errors++;
            report.ErrorMessages.Add($"Step 2 (Reconciliation): {ex.Message}");
            _logger.LogError(ex, "EOD Step 2 failed");
        }

        // ── Step 3: Portfolio Summary ───────────────────────────────────────
        try
        {
            report.TotalActiveLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Active, ct);
            report.TotalOverdueLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Overdue, ct);
            report.TotalClosedLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Closed, ct);

            report.TotalDisbursedToday = await _db.LoanDisbursements
                .Where(d => d.DisbursementDate == businessDate)
                .SumAsync(d => d.DisbursedAmount, ct);

            report.TotalPaymentsToday = await _db.Payments
                .Where(p => p.PaymentDate == businessDate && p.Amount > 0)
                .SumAsync(p => p.Amount, ct);
        }
        catch (Exception ex)
        {
            report.Errors++;
            report.ErrorMessages.Add($"Step 3 (Portfolio Summary): {ex.Message}");
            _logger.LogError(ex, "EOD Step 3 failed");
        }

        // ── Step 4: Notifications & Reminders ───────────────────────────────
        try
        {
            await _notificationService.GenerateDueRemindersAsync(businessDate);
            await _notificationService.GenerateOverdueRemindersAsync(businessDate);
            _logger.LogInformation("EOD Step 4 (Notifications) completed.");
        }
        catch (Exception ex)
        {
            report.Errors++;
            report.ErrorMessages.Add($"Step 4 (Notifications): {ex.Message}");
            _logger.LogError(ex, "EOD Step 4 failed");
        }

        report.CompletedAt = DateTime.UtcNow;

        // EOD audit log
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.EodCompleted,
            EntityType = "EOD",
            EntityId = 0,
            NewValuesJson = AuditHelper.ToJson(new
            {
                businessDate = businessDate.ToString(),
                duration = report.Duration.TotalSeconds,
                errors = report.Errors,
                activeLoans = report.TotalActiveLoans,
                overdueLoans = report.TotalOverdueLoans
            })
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EOD completed for {Date} in {Duration:F2}s — {Errors} errors",
            businessDate, report.Duration.TotalSeconds, report.Errors);

        return report;
    }
}
