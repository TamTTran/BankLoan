using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

// ── DTOs for job results ────────────────────────────────────────────────────
public class OverdueJobResultDto
{
    public DateOnly BusinessDate { get; set; }
    public int TotalLoansProcessed { get; set; }
    public int LoansMarkedOverdue { get; set; }
    public int LoansRevertedToActive { get; set; }
    public int SnapshotsCreated { get; set; }
    public int SnapshotsUpdated { get; set; }
    public int LateFeesCalculated { get; set; }
    public int Errors { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SnapshotSummaryDto
{
    public DateOnly SnapshotDate { get; set; }
    public int TotalOverdueLoans { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public decimal TotalLateFeeAccrued { get; set; }
    public List<BucketSummaryItem> BucketBreakdown { get; set; } = [];
}

public class BucketSummaryItem
{
    public string Bucket { get; set; } = default!;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IOverdueJobService
{
    /// <summary>
    /// Run the daily overdue processing for the given business date.
    /// Idempotent: safe to call multiple times for the same date.
    /// </summary>
    Task<OverdueJobResultDto> RunDailyOverdueProcessingAsync(DateOnly businessDate, int batchSize = 100, CancellationToken ct = default);

    /// <summary>
    /// Query snapshot history for a date range.
    /// </summary>
    Task<List<SnapshotSummaryDto>> GetSnapshotHistoryAsync(DateOnly from, DateOnly to);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class OverdueJobService : IOverdueJobService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILateFeeService _lateFeeService;
    private readonly ILogger<OverdueJobService> _logger;
    private readonly IBusinessClock _businessClock;

    public OverdueJobService(LoanFlowDbContext db, ILateFeeService lateFeeService, ILogger<OverdueJobService> logger, IBusinessClock businessClock)
    {
        _db = db;
        _lateFeeService = lateFeeService;
        _logger = logger;
        _businessClock = businessClock;
    }

    public async Task<OverdueJobResultDto> RunDailyOverdueProcessingAsync(
        DateOnly businessDate, int batchSize = 100, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new OverdueJobResultDto { BusinessDate = businessDate };

        _logger.LogInformation("Overdue job started for business date {Date}", businessDate);

        // Load existing snapshots for today to support idempotent upsert
        var existingSnapshots = await _db.OverdueDailySnapshots
            .Where(s => s.SnapshotDate == businessDate)
            .ToDictionaryAsync(s => s.LoanId, ct);

        // Process in batches to avoid loading the entire portfolio
        int skip = 0;
        bool hasMore = true;

        while (hasMore && !ct.IsCancellationRequested)
        {
            var loans = await _db.Loans
                .Include(l => l.RepaymentSchedules)
                .Include(l => l.LateFees)
                .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
                .OrderBy(l => l.Id)
                .Skip(skip)
                .Take(batchSize)
                .AsSplitQuery()
                .ToListAsync(ct);

            if (loans.Count == 0)
            {
                hasMore = false;
                break;
            }

            foreach (var loan in loans)
            {
                try
                {
                    await ProcessSingleLoanAsync(loan, businessDate, existingSnapshots, result);
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    _logger.LogError(ex, "Error processing loan {LoanId} for overdue job", loan.Id);
                }
            }

            // Save after each batch
            await SaveChangesWithConcurrencyHandlingAsync(ct);

            skip += batchSize;
            result.TotalLoansProcessed += loans.Count;

            if (loans.Count < batchSize)
                hasMore = false;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;

        _logger.LogInformation(
            "Overdue job completed for {Date}: {Processed} loans processed, {Overdue} marked overdue, " +
            "{Reverted} reverted to active, {Snapshots} snapshots created, {Updated} updated, {Errors} errors. Duration: {Duration}",
            businessDate, result.TotalLoansProcessed, result.LoansMarkedOverdue,
            result.LoansRevertedToActive, result.SnapshotsCreated, result.SnapshotsUpdated,
            result.Errors, result.Duration);

        return result;
    }

    private async Task ProcessSingleLoanAsync(
        Loan loan, DateOnly businessDate,
        Dictionary<int, OverdueDailySnapshot> existingSnapshots,
        OverdueJobResultDto result)
    {
        var overdueSchedules = loan.RepaymentSchedules
            .Where(s => s.DueDate < businessDate && s.RemainingDueAmount > 0)
            .ToList();

        // ── Update installment statuses ──────────────────────────────────
        foreach (var schedule in loan.RepaymentSchedules)
        {
            if (schedule.Status == InstallmentStatus.Paid) continue;

            if (schedule.DueDate < businessDate && schedule.RemainingDueAmount > 0)
            {
                schedule.Status = InstallmentStatus.Overdue;
            }
        }

        // ── Update loan status ───────────────────────────────────────────
        if (overdueSchedules.Count > 0)
        {
            if (loan.Status == LoanStatus.Active)
            {
                loan.Status = LoanStatus.Overdue;
                loan.UpdatedAt = _businessClock.UtcNow;
                result.LoansMarkedOverdue++;
            }
        }
        else
        {
            // No overdue installments — revert to Active if was Overdue
            if (loan.Status == LoanStatus.Overdue)
            {
                loan.Status = LoanStatus.Active;
                loan.UpdatedAt = _businessClock.UtcNow;
                result.LoansRevertedToActive++;
            }
            // No overdue → no snapshot needed for this loan
            return;
        }

        // ── Calculate late fees ──────────────────────────────────────────
        await _lateFeeService.CalculateLateFeesAsync(loan, businessDate);
        result.LateFeesCalculated++;

        // ── Create/update snapshot ───────────────────────────────────────
        var oldestDue = overdueSchedules.Min(s => s.DueDate);
        int dpd = businessDate.DayNumber - oldestDue.DayNumber;
        var overdueAmount = overdueSchedules.Sum(s => s.RemainingDueAmount);
        var pendingLateFees = loan.LateFees
            .Where(f => f.Status == LateFeeStatus.Pending)
            .Sum(f => f.FeeAmount);

        if (existingSnapshots.TryGetValue(loan.Id, out var existing))
        {
            // Update existing snapshot (idempotent)
            existing.Dpd = dpd;
            existing.Bucket = DpdBucket.Calculate(dpd);
            existing.OverdueAmount = overdueAmount;
            existing.OutstandingAmount = loan.OutstandingAmount;
            existing.LateFeeAccrued = pendingLateFees;
            existing.OverdueInstallments = overdueSchedules.Count;
            result.SnapshotsUpdated++;
        }
        else
        {
            // Create new snapshot
            var snapshot = new OverdueDailySnapshot
            {
                LoanId = loan.Id,
                SnapshotDate = businessDate,
                Dpd = dpd,
                Bucket = DpdBucket.Calculate(dpd),
                OverdueAmount = overdueAmount,
                OutstandingAmount = loan.OutstandingAmount,
                LateFeeAccrued = pendingLateFees,
                OverdueInstallments = overdueSchedules.Count
            };
            _db.OverdueDailySnapshots.Add(snapshot);
            existingSnapshots[loan.Id] = snapshot; // track for idempotency within this run
            result.SnapshotsCreated++;
        }
    }

    public async Task<List<SnapshotSummaryDto>> GetSnapshotHistoryAsync(DateOnly from, DateOnly to)
    {
        var snapshots = await _db.OverdueDailySnapshots
            .Where(s => s.SnapshotDate >= from && s.SnapshotDate <= to)
            .GroupBy(s => s.SnapshotDate)
            .Select(g => new SnapshotSummaryDto
            {
                SnapshotDate = g.Key,
                TotalOverdueLoans = g.Count(),
                TotalOverdueAmount = g.Sum(s => s.OverdueAmount),
                TotalLateFeeAccrued = g.Sum(s => s.LateFeeAccrued),
                BucketBreakdown = g.GroupBy(s => s.Bucket)
                    .Select(bg => new BucketSummaryItem
                    {
                        Bucket = bg.Key,
                        Count = bg.Count(),
                        Amount = bg.Sum(s => s.OverdueAmount)
                    }).ToList()
            })
            .OrderByDescending(s => s.SnapshotDate)
            .ToListAsync();

        return snapshots;
    }

    private async Task SaveChangesWithConcurrencyHandlingAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Overdue processing conflicted with concurrent loan updates. Re-run the job for the same business date.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry") == true)
        {
            // Snapshot for today already exists (e.g. API restarted mid-day).
            // Detach tracked-but-duplicate entities and continue gracefully.
            _logger.LogWarning("Duplicate snapshot detected — skipping duplicates (idempotent re-run). Details: {Msg}", ex.InnerException.Message);
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added))
            {
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
        }
    }
}
