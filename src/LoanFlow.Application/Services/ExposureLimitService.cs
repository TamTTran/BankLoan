using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class ExposureLimitDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal MaxExposure { get; set; }
    public decimal CurrentExposure { get; set; }
    public decimal UtilizationRate { get; set; }
    public string Status { get; set; } = "";
}

public class SetExposureLimitRequest
{
    public decimal MaxExposure { get; set; }
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IExposureLimitService
{
    Task<Result<ExposureLimitDto>> SetLimitAsync(int customerId, decimal maxExposure, int userId);
    Task<ExposureLimitDto?> GetLimitAsync(int customerId);
    Task<Result> CheckExposureAsync(int customerId, decimal additionalAmount);
    Task<Result> OverrideExposureAsync(int customerId, string reason, int userId);
    Task RefreshExposureAsync(int customerId);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class ExposureLimitService : IExposureLimitService
{
    private readonly LoanFlowDbContext _db;

    public ExposureLimitService(LoanFlowDbContext db) => _db = db;

    public async Task<Result<ExposureLimitDto>> SetLimitAsync(int customerId, decimal maxExposure, int userId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return Result.Failure<ExposureLimitDto>("Customer not found.");

        var currentExposure = await _db.Loans
            .Where(l => l.CustomerId == customerId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .SumAsync(l => l.OutstandingAmount);

        var limit = await _db.Set<CustomerExposureLimit>().FirstOrDefaultAsync(e => e.CustomerId == customerId);
        if (limit == null)
        {
            limit = new CustomerExposureLimit { CustomerId = customerId };
            _db.Set<CustomerExposureLimit>().Add(limit);
        }

        limit.MaxExposure = maxExposure;
        limit.CurrentExposure = currentExposure;
        limit.UtilizationRate = maxExposure > 0 ? Math.Round(currentExposure / maxExposure * 100, 2) : 0;
        limit.Status = limit.UtilizationRate >= 100 ? "Exceeded" : limit.UtilizationRate >= 80 ? "Warning" : "Normal";
        limit.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Result.Success(new ExposureLimitDto
        {
            CustomerId = customerId, CustomerName = customer.FullName,
            MaxExposure = limit.MaxExposure, CurrentExposure = limit.CurrentExposure,
            UtilizationRate = limit.UtilizationRate, Status = limit.Status
        });
    }

    public async Task<ExposureLimitDto?> GetLimitAsync(int customerId)
    {
        var limit = await _db.Set<CustomerExposureLimit>()
            .Include(e => e.Customer)
            .FirstOrDefaultAsync(e => e.CustomerId == customerId);
        if (limit == null) return null;

        return new ExposureLimitDto
        {
            CustomerId = limit.CustomerId, CustomerName = limit.Customer.FullName,
            MaxExposure = limit.MaxExposure, CurrentExposure = limit.CurrentExposure,
            UtilizationRate = limit.UtilizationRate, Status = limit.Status
        };
    }

    public async Task<Result> CheckExposureAsync(int customerId, decimal additionalAmount)
    {
        var limit = await _db.Set<CustomerExposureLimit>().FirstOrDefaultAsync(e => e.CustomerId == customerId);
        if (limit == null) return Result.Success(); // No limit set → allow

        if (limit.Status == "Overridden") return Result.Success();

        var projectedExposure = limit.CurrentExposure + additionalAmount;
        if (projectedExposure > limit.MaxExposure)
            return Result.Failure($"Exposure limit exceeded. Max: {limit.MaxExposure:N0}, Current: {limit.CurrentExposure:N0}, Requested: {additionalAmount:N0}");

        return Result.Success();
    }

    public async Task<Result> OverrideExposureAsync(int customerId, string reason, int userId)
    {
        var limit = await _db.Set<CustomerExposureLimit>().FirstOrDefaultAsync(e => e.CustomerId == customerId);
        if (limit == null) return Result.Failure("No exposure limit set for this customer.");

        limit.Status = "Overridden";
        limit.OverriddenByUserId = userId;
        limit.OverrideReason = reason;
        limit.OverriddenAt = DateTime.UtcNow;
        limit.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId, ActionCode = "EXPOSURE_OVERRIDDEN",
            EntityType = "CustomerExposureLimit", EntityId = limit.Id,
            NewValuesJson = AuditHelper.ToJson(new { customerId, reason })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task RefreshExposureAsync(int customerId)
    {
        var limit = await _db.Set<CustomerExposureLimit>().FirstOrDefaultAsync(e => e.CustomerId == customerId);
        if (limit == null) return;

        limit.CurrentExposure = await _db.Loans
            .Where(l => l.CustomerId == customerId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .SumAsync(l => l.OutstandingAmount);
        limit.UtilizationRate = limit.MaxExposure > 0 ? Math.Round(limit.CurrentExposure / limit.MaxExposure * 100, 2) : 0;
        limit.Status = limit.Status == "Overridden" ? "Overridden"
            : limit.UtilizationRate >= 100 ? "Exceeded"
            : limit.UtilizationRate >= 80 ? "Warning" : "Normal";
        limit.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
