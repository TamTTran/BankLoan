using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class CollateralLifecycleService : ICollateralLifecycleService
{
    private readonly LoanFlowDbContext _db;

    public CollateralLifecycleService(LoanFlowDbContext db) => _db = db;

    public async Task<Result<RevaluationDto>> RevalueAsync(int collateralId, RevalueRequest req, int userId)
    {
        var collateral = await _db.Collaterals.FindAsync(collateralId);
        if (collateral == null) return Result.Failure<RevaluationDto>("Collateral not found.");

        var reval = new CollateralRevaluation
        {
            CollateralId = collateralId,
            OldValue = collateral.AppraisedValue,
            NewValue = req.NewValue,
            RevaluedBy = req.RevaluedBy,
            Reason = req.Reason,
            RecordedByUserId = userId
        };

        collateral.AppraisedValue = req.NewValue;
        collateral.AppraisalDate = DateTime.UtcNow;
        collateral.AppraisedBy = req.RevaluedBy;
        collateral.UpdatedAt = DateTime.UtcNow;

        _db.Set<CollateralRevaluation>().Add(reval);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId, ActionCode = "COLLATERAL_REVALUED",
            EntityType = "Collateral", EntityId = collateralId,
            NewValuesJson = AuditHelper.ToJson(new { oldValue = reval.OldValue, newValue = reval.NewValue })
        });

        await _db.SaveChangesAsync();

        return Result.Success(new RevaluationDto
        {
            Id = reval.Id, CollateralId = collateralId,
            OldValue = reval.OldValue, NewValue = reval.NewValue,
            RevaluedBy = reval.RevaluedBy, Reason = reval.Reason, RevaluationDate = reval.RevaluationDate
        });
    }

    public async Task<List<RevaluationDto>> GetRevaluationHistoryAsync(int collateralId)
    {
        return await _db.Set<CollateralRevaluation>()
            .Where(r => r.CollateralId == collateralId)
            .OrderByDescending(r => r.RevaluationDate)
            .Select(r => new RevaluationDto
            {
                Id = r.Id, CollateralId = r.CollateralId,
                OldValue = r.OldValue, NewValue = r.NewValue,
                RevaluedBy = r.RevaluedBy, Reason = r.Reason, RevaluationDate = r.RevaluationDate
            }).ToListAsync();
    }

    public async Task<Result> ReleaseCollateralAsync(int collateralId, int userId)
    {
        var collateral = await _db.Collaterals.Include(c => c.LoanCollaterals).FirstOrDefaultAsync(c => c.Id == collateralId);
        if (collateral == null) return Result.Failure("Collateral not found.");
        if (collateral.LoanCollaterals.Any(lc => lc.Status == "Active"))
            return Result.Failure("Cannot release collateral still securing active loans.");

        collateral.Status = "Released";
        collateral.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> SeizeCollateralAsync(int collateralId, int userId)
    {
        var collateral = await _db.Collaterals.FindAsync(collateralId);
        if (collateral == null) return Result.Failure("Collateral not found.");

        collateral.Status = "Seized";
        collateral.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId, ActionCode = "COLLATERAL_SEIZED",
            EntityType = "Collateral", EntityId = collateralId
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }
}
