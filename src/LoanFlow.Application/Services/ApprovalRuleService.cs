using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class ApprovalRuleDto
{
    public int Id { get; set; }
    public string? ProductType { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int RequiredLevel { get; set; }
    public string ApproverRole { get; set; } = default!;
    public int? MinCicScore { get; set; }
    public bool IsAutoApprove { get; set; }
}

public class CreateApprovalRuleRequest
{
    public string? ProductType { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int RequiredLevel { get; set; }
    public string ApproverRole { get; set; } = default!;
    public int? MinCicScore { get; set; }
    public bool IsAutoApprove { get; set; }
}

public interface IApprovalRuleService
{
    Task<IEnumerable<ApprovalRuleDto>> GetAllAsync();
    Task<Result<ApprovalRuleDto>> CreateAsync(CreateApprovalRuleRequest request);
    Task<Result> DeleteAsync(int id);
    Task<IEnumerable<ApprovalRuleDto>> GetRequiredRulesForApplicationAsync(int applicationId);
}

public class ApprovalRuleService : IApprovalRuleService
{
    private readonly LoanFlowDbContext _db;

    public ApprovalRuleService(LoanFlowDbContext db) => _db = db;

    public async Task<IEnumerable<ApprovalRuleDto>> GetAllAsync()
    {
        return await _db.ApprovalRules
            .OrderBy(r => r.RequiredLevel)
            .ThenBy(r => r.MinAmount)
            .Select(r => new ApprovalRuleDto
            {
                Id = r.Id, ProductType = r.ProductType, MinAmount = r.MinAmount,
                MaxAmount = r.MaxAmount, RequiredLevel = r.RequiredLevel, ApproverRole = r.ApproverRole,
                MinCicScore = r.MinCicScore, IsAutoApprove = r.IsAutoApprove
            })
            .ToListAsync();
    }

    public async Task<Result<ApprovalRuleDto>> CreateAsync(CreateApprovalRuleRequest req)
    {
        if (req.MinAmount >= req.MaxAmount)
            return Result.Failure<ApprovalRuleDto>("MinAmount must be less than MaxAmount");

        var entity = new ApprovalRule
        {
            ProductType = req.ProductType,
            MinAmount = req.MinAmount,
            MaxAmount = req.MaxAmount,
            RequiredLevel = req.RequiredLevel,
            ApproverRole = req.ApproverRole,
            MinCicScore = req.MinCicScore,
            IsAutoApprove = req.IsAutoApprove
        };

        _db.ApprovalRules.Add(entity);
        await _db.SaveChangesAsync();

        return Result.Success(new ApprovalRuleDto
        {
            Id = entity.Id, ProductType = entity.ProductType, MinAmount = entity.MinAmount,
            MaxAmount = entity.MaxAmount, RequiredLevel = entity.RequiredLevel, ApproverRole = entity.ApproverRole,
            MinCicScore = entity.MinCicScore, IsAutoApprove = entity.IsAutoApprove
        });
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.ApprovalRules.FindAsync(id);
        if (entity == null) return Result.Failure("Rule not found");
        
        _db.ApprovalRules.Remove(entity);
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<IEnumerable<ApprovalRuleDto>> GetRequiredRulesForApplicationAsync(int applicationId)
    {
        var app = await _db.LoanApplications
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null) return Enumerable.Empty<ApprovalRuleDto>();

        // Find rules that match the product type (or null for all) AND the amount falls into [Min, Max]
        var matchedRules = await _db.ApprovalRules
            .Where(r => (r.ProductType == null || r.ProductType == app.Product.ProductType)
                     && (app.RequestedAmount >= r.MinAmount && app.RequestedAmount <= r.MaxAmount))
            .OrderBy(r => r.RequiredLevel)
            .Select(r => new ApprovalRuleDto
            {
                Id = r.Id, ProductType = r.ProductType, MinAmount = r.MinAmount,
                MaxAmount = r.MaxAmount, RequiredLevel = r.RequiredLevel, ApproverRole = r.ApproverRole,
                MinCicScore = r.MinCicScore, IsAutoApprove = r.IsAutoApprove
            })
            .ToListAsync();

        return matchedRules;
    }
}
