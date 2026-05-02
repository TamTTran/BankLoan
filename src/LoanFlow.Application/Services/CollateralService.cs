using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Collateral;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class CollateralService : ICollateralService
{
    private readonly LoanFlowDbContext _db;

    public CollateralService(LoanFlowDbContext db) => _db = db;

    public async Task<IEnumerable<CollateralDto>> GetByCustomerIdAsync(int customerId)
    {
        return await _db.Collaterals
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => MapToDto(c))
            .ToListAsync();
    }

    public async Task<Result<CollateralDto>> GetByIdAsync(int id)
    {
        var entity = await _db.Collaterals.FindAsync(id);
        if (entity is null) return Result.Failure<CollateralDto>("Collateral not found.");
        return Result.Success(MapToDto(entity));
    }

    public async Task<Result<CollateralDto>> CreateAsync(CreateCollateralRequest req)
    {
        var customer = await _db.Customers.FindAsync(req.CustomerId);
        if (customer is null) return Result.Failure<CollateralDto>("Customer not found.");

        var entity = new Collateral
        {
            CustomerId = req.CustomerId,
            CollateralType = req.CollateralType,
            Description = req.Description,
            RegistrationNo = req.RegistrationNo,
            AppraisedValue = req.AppraisedValue,
            AppraisedBy = req.AppraisedBy,
            Address = req.Address,
            Status = CollateralStatus.Active
        };

        _db.Collaterals.Add(entity);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = customer.CreatedByUserId, // System/app user logic should ideally supply actor user ID to method
            ActionCode = AuditActions.CollateralCreated,
            EntityType = "Collateral",
            EntityId = entity.Id,
            NewValuesJson = AuditHelper.ToJson(new { customerId = entity.CustomerId, value = entity.AppraisedValue })
        });
        await _db.SaveChangesAsync();

        return Result.Success(MapToDto(entity));
    }

    public async Task<Result<CollateralDto>> UpdateAsync(int id, UpdateCollateralRequest req)
    {
        var entity = await _db.Collaterals.FindAsync(id);
        if (entity is null) return Result.Failure<CollateralDto>("Collateral not found.");

        entity.CollateralType = req.CollateralType;
        entity.Description = req.Description;
        entity.RegistrationNo = req.RegistrationNo;
        entity.AppraisedValue = req.AppraisedValue;
        entity.AppraisedBy = req.AppraisedBy;
        entity.Address = req.Address;
        entity.Status = req.Status;

        await _db.SaveChangesAsync();
        return Result.Success(MapToDto(entity));
    }

    public async Task<IEnumerable<LoanCollateralDto>> GetByLoanIdAsync(int loanId)
    {
        return await _db.LoanCollaterals
            .Include(lc => lc.Collateral)
            .Where(lc => lc.LoanId == loanId)
            .Select(lc => new LoanCollateralDto
            {
                Id = lc.Id,
                LoanId = lc.LoanId,
                CollateralId = lc.CollateralId,
                AllocatedValue = lc.AllocatedValue,
                Collateral = MapToDto(lc.Collateral)
            })
            .ToListAsync();
    }

    public async Task<Result> AllocateToLoanAsync(int loanId, AllocateCollateralRequest req)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure("Loan not found.");

        var collateral = await _db.Collaterals.FindAsync(req.CollateralId);
        if (collateral is null) return Result.Failure("Collateral not found.");
        if (collateral.Status != CollateralStatus.Active) return Result.Failure("Collateral is not active.");

        // Check if collateral belongs to the loan's customer (for simplicity, but could belong to a guarantor)
        if (collateral.CustomerId != loan.CustomerId) 
            return Result.Failure("Collateral does not belong to the loan's customer.");

        // Calculate how much of this collateral is already allocated to other loans
        var sumAllocated = await _db.LoanCollaterals
            .Where(lc => lc.CollateralId == collateral.Id)
            .SumAsync(lc => lc.AllocatedValue);

        var availableValue = collateral.AppraisedValue - sumAllocated;
        if (req.AllocatedValue > availableValue)
            return Result.Failure($"Requested allocation ({req.AllocatedValue}) exceeds available collateral value ({availableValue}).");

        // Idempotent/Update logic
        var existing = await _db.LoanCollaterals
            .FirstOrDefaultAsync(lc => lc.LoanId == loanId && lc.CollateralId == req.CollateralId);

        if (existing != null)
        {
            // Compute difference to ensure we don't exceed availability overall
            var diff = req.AllocatedValue - existing.AllocatedValue;
            if (diff > availableValue) return Result.Failure("Exceeds available value on update.");
            existing.AllocatedValue = req.AllocatedValue;
        }
        else
        {
            _db.LoanCollaterals.Add(new LoanCollateral
            {
                LoanId = loanId,
                CollateralId = collateral.Id,
                AllocatedValue = req.AllocatedValue
            });
        }

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> RemoveFromLoanAsync(int loanId, int collateralId)
    {
        var existing = await _db.LoanCollaterals
            .FirstOrDefaultAsync(lc => lc.LoanId == loanId && lc.CollateralId == collateralId);
            
        if (existing is null) return Result.Failure("Collateral not allocated to this loan.");

        _db.LoanCollaterals.Remove(existing);
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private static CollateralDto MapToDto(Collateral c) => new()
    {
        Id = c.Id,
        CustomerId = c.CustomerId,
        CollateralType = c.CollateralType,
        Description = c.Description,
        RegistrationNo = c.RegistrationNo,
        AppraisedValue = c.AppraisedValue,
        AppraisedBy = c.AppraisedBy,
        Address = c.Address,
        Status = c.Status,
        CreatedAt = c.CreatedAt
    };
}
