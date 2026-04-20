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
    private readonly IAccrualJobService _accrualService;
    private readonly IOverdueJobService _overdueService;
    private readonly IClassificationJobService _classificationService;
    private readonly IReconciliationService _reconciliationService;
    private readonly ICollectionService _collectionService;
    private readonly INotificationService _notificationService;
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<EndOfDayService> _logger;
    private readonly IBusinessClock _businessClock;

    public EndOfDayService(
        IAccrualJobService accrualService,
        IOverdueJobService overdueService,
        IClassificationJobService classificationService,
        IReconciliationService reconciliationService,
        ICollectionService collectionService,
        INotificationService notificationService,
        LoanFlowDbContext db,
        ILogger<EndOfDayService> logger,
        IBusinessClock businessClock)
    {
        _accrualService = accrualService;
        _overdueService = overdueService;
        _classificationService = classificationService;
        _reconciliationService = reconciliationService;
        _collectionService = collectionService;
        _notificationService = notificationService;
        _db = db;
        _logger = logger;
        _businessClock = businessClock;
    }

    private async Task SetSystemLockAsync(bool isLocked, CancellationToken ct)
    {
        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "IsEodRunning", ct);
        if (config == null)
        {
            config = new SystemConfig { Key = "IsEodRunning" };
            _db.SystemConfigs.Add(config);
        }
        config.Value = isLocked ? "true" : "false";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EodReportDto> RunAsync(DateOnly businessDate, int? userId = null, CancellationToken ct = default)
    {
        var report = new EodReportDto { BusinessDate = businessDate, StartedAt = _businessClock.UtcNow };
        _logger.LogInformation("EOD processing started for {Date}", businessDate);

        // Fetch or create EodBatchRecord
        var batch = await _db.EodBatchRecords.FirstOrDefaultAsync(b => b.BusinessDate == businessDate, ct);
        if (batch == null)
        {
            batch = new EodBatchRecord { BusinessDate = businessDate, StartedAt = DateTime.UtcNow, Status = "Running" };
            _db.EodBatchRecords.Add(batch);
        }
        else if (batch.Status == "Completed")
        {
            _logger.LogWarning("EOD already completed for {date}.", businessDate);
            return report;
        }
        else
        {
            batch.Status = "Running"; // resumed
        }
        await _db.SaveChangesAsync(ct);

        // Step 0: Lock System
        await SetSystemLockAsync(true, ct);

        try
        {
            // Step 1: Reconciliation
            if (batch.LastCompletedStep < 1)
            {
                _logger.LogInformation("EOD Step 1: Reconciliation");
                report.ReconciliationResult = await _reconciliationService.RunReconciliationAsync(businessDate, userId);
                batch.LastCompletedStep = 1;
                await _db.SaveChangesAsync(ct);
            }

            // Step 2: Accrual
            if (batch.LastCompletedStep < 2)
            {
                _logger.LogInformation("EOD Step 2: Accrual");
                await _accrualService.RunDailyAccrualAsync(businessDate, ct);
                batch.LastCompletedStep = 2;
                await _db.SaveChangesAsync(ct);
            }

            // Step 3: Overdue & Late Fees
            if (batch.LastCompletedStep < 3)
            {
                _logger.LogInformation("EOD Step 3: Overdue Processing");
                report.OverdueResult = await _overdueService.RunDailyOverdueProcessingAsync(businessDate, ct: ct);
                report.BrokenPromises = await _collectionService.CheckBrokenPromisesAsync(businessDate);
                batch.LastCompletedStep = 3;
                await _db.SaveChangesAsync(ct);
            }

            // Step 4: Classification
            if (batch.LastCompletedStep < 4)
            {
                _logger.LogInformation("EOD Step 4: Classification & Provision");
                await _classificationService.RunDailyClassificationAsync(businessDate, ct);
                batch.LastCompletedStep = 4;
                await _db.SaveChangesAsync(ct);
            }

            // Step 5: Notifications & Summary
            if (batch.LastCompletedStep < 5)
            {
                _logger.LogInformation("EOD Step 5: Summaries & Notifications");
                await _notificationService.GenerateDueRemindersAsync(businessDate);
                await _notificationService.GenerateOverdueRemindersAsync(businessDate);

                report.TotalActiveLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Active, ct);
                report.TotalOverdueLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Overdue, ct);
                report.TotalClosedLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Closed, ct);

                batch.LastCompletedStep = 5;
                await _db.SaveChangesAsync(ct);
            }

            // Completed Successfully
            batch.Status = "Completed";
            batch.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Step 6: Unlock System
            await SetSystemLockAsync(false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EOD failed at step {Step}", batch.LastCompletedStep + 1);
            batch.Status = "Failed";
            batch.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
            
            // SYSTEM REMAINS LOCKED SO IT CANNOT BE CORRUPTED BY USERS!!
            report.Errors++;
            report.ErrorMessages.Add($"Exception: {ex.Message}");
        }

        report.CompletedAt = _businessClock.UtcNow;

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
