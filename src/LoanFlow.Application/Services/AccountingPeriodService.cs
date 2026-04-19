using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class AccountingPeriodDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByName { get; set; }
    public string? Notes { get; set; }
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IAccountingPeriodService
{
    Task<List<AccountingPeriodDto>> GetAllPeriodsAsync();
    Task<AccountingPeriodDto?> GetCurrentPeriodAsync();
    Task<Result> ClosePeriodAsync(int year, int month, int userId, string? notes = null);
    Task<Result> ReopenPeriodAsync(int year, int month, int userId);
    Task<bool> IsPeriodOpenAsync(DateOnly businessDate);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class AccountingPeriodService : IAccountingPeriodService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessClock _clock;

    public AccountingPeriodService(LoanFlowDbContext db, IBusinessClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<AccountingPeriodDto>> GetAllPeriodsAsync()
    {
        return await _db.Set<AccountingPeriod>()
            .Include(p => p.ClosedBy)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Select(p => new AccountingPeriodDto
            {
                Id = p.Id, Year = p.Year, Month = p.Month,
                Status = p.Status, OpenedAt = p.OpenedAt,
                ClosedAt = p.ClosedAt,
                ClosedByName = p.ClosedBy != null ? p.ClosedBy.FullName : null,
                Notes = p.Notes
            })
            .ToListAsync();
    }

    public async Task<AccountingPeriodDto?> GetCurrentPeriodAsync()
    {
        var period = await _db.Set<AccountingPeriod>()
            .Include(p => p.ClosedBy)
            .Where(p => p.Status == "Open")
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync();

        if (period == null) return null;

        return new AccountingPeriodDto
        {
            Id = period.Id, Year = period.Year, Month = period.Month,
            Status = period.Status, OpenedAt = period.OpenedAt,
            ClosedAt = period.ClosedAt,
            ClosedByName = period.ClosedBy?.FullName,
            Notes = period.Notes
        };
    }

    public async Task<Result> ClosePeriodAsync(int year, int month, int userId, string? notes = null)
    {
        var period = await _db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.Year == year && p.Month == month);

        if (period == null)
            return Result.Failure($"Accounting period {year}/{month:D2} not found.");

        if (period.Status == "Closed")
            return Result.Failure($"Period {year}/{month:D2} is already closed.");

        // Mark as closed
        period.Status = "Closed";
        period.ClosedAt = _clock.UtcNow;
        period.ClosedByUserId = userId;
        period.Notes = notes;

        // Auto-open next period if not exists
        var nextYear = month == 12 ? year + 1 : year;
        var nextMonth = month == 12 ? 1 : month + 1;

        var nextPeriod = await _db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.Year == nextYear && p.Month == nextMonth);

        if (nextPeriod == null)
        {
            _db.Set<AccountingPeriod>().Add(new AccountingPeriod
            {
                Year = nextYear,
                Month = nextMonth,
                Status = "Open",
                OpenedAt = _clock.UtcNow
            });
        }

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = "PERIOD_CLOSED",
            EntityType = "AccountingPeriod",
            EntityId = period.Id,
            NewValuesJson = AuditHelper.ToJson(new { year, month, notes })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ReopenPeriodAsync(int year, int month, int userId)
    {
        var period = await _db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.Year == year && p.Month == month);

        if (period == null)
            return Result.Failure($"Accounting period {year}/{month:D2} not found.");

        if (period.Status != "Closed")
            return Result.Failure($"Period {year}/{month:D2} is not closed.");

        period.Status = "Open";
        period.ClosedAt = null;
        period.ClosedByUserId = null;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = "PERIOD_REOPENED",
            EntityType = "AccountingPeriod",
            EntityId = period.Id,
            NewValuesJson = AuditHelper.ToJson(new { year, month })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<bool> IsPeriodOpenAsync(DateOnly businessDate)
    {
        var period = await _db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.Year == businessDate.Year && p.Month == businessDate.Month);

        // If period doesn't exist yet, treat as open (auto-created on first use)
        return period == null || period.Status == "Open";
    }
}
