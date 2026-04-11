using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Customer;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetListAsync(CustomerFilterRequest filter);
    Task<Result<CustomerDto>> GetByIdAsync(int id);
    Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, int createdByUserId);
    Task<Result<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest request);
    Task<Result<CustomerDto>> CheckCreditStatusAsync(int id, int checkedByUserId);
}

public class CustomerService : ICustomerService
{
    private readonly LoanFlowDbContext _db;
    private readonly ICreditCheckService _creditCheckService;

    public CustomerService(LoanFlowDbContext db, ICreditCheckService creditCheckService)
    {
        _db = db;
        _creditCheckService = creditCheckService;
    }

    public async Task<PagedResult<CustomerListItemDto>> GetListAsync(CustomerFilterRequest filter)
    {
        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(s) ||
                c.Phone.Contains(s) ||
                c.IdNumber.Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                FullName = c.FullName,
                MaskedPhone = c.Phone.MaskPhone(),
                MaskedIdNumber = c.IdNumber.MaskIdNumber(),
                Status = c.Status,
                CreditScore = c.CreditScore,
                CreditStatus = c.CreditStatus,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<CustomerListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<Result<CustomerDto>> GetByIdAsync(int id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return Result.Failure<CustomerDto>("Customer not found.");
        return Result.Success(MapToDto(c));
    }

    public async Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest req, int createdByUserId)
    {
        // CUS-VAL-001
        if (await _db.Customers.AnyAsync(c => c.Phone == req.Phone))
            return Result.Failure<CustomerDto>("CUS-VAL-001: Phone number already exists.");

        // CUS-VAL-002
        if (await _db.Customers.AnyAsync(c => c.IdNumber == req.IdNumber))
            return Result.Failure<CustomerDto>("CUS-VAL-002: ID number already exists.");

        var entity = new Customer
        {
            FullName = req.FullName,
            Phone = req.Phone,
            IdNumber = req.IdNumber,
            Address = req.Address,
            DateOfBirth = req.DateOfBirth.ToDateTime(TimeOnly.MinValue),
            Status = "Active",
            CreatedByUserId = createdByUserId
        };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = createdByUserId,
            ActionCode = AuditActions.CustomerCreated,
            EntityType = "Customer",
            EntityId = entity.Id,
            NewValuesJson = AuditHelper.ToJson(new { fullName = entity.FullName, phone = entity.Phone })
        });
        await _db.SaveChangesAsync();

        return await GetByIdAsync(entity.Id);
    }

    public async Task<Result<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest req)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return Result.Failure<CustomerDto>("Customer not found.");

        // Uniqueness still enforced on update
        if (entity.Phone != req.Phone && await _db.Customers.AnyAsync(c => c.Phone == req.Phone && c.Id != id))
            return Result.Failure<CustomerDto>("CUS-VAL-001: Phone number already exists.");

        var oldPhone = entity.Phone;
        entity.FullName = req.FullName;
        entity.Phone = req.Phone;
        entity.Address = req.Address;
        entity.DateOfBirth = req.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActionCode = AuditActions.CustomerUpdated,
            EntityType = "Customer",
            EntityId = entity.Id,
            OldValuesJson = AuditHelper.ToJson(new { phone = oldPhone }),
            NewValuesJson = AuditHelper.ToJson(new { phone = entity.Phone })
        });
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<Result<CustomerDto>> CheckCreditStatusAsync(int id, int checkedByUserId)
    {
        var result = await _creditCheckService.PerformCreditCheckAsync(id, null, checkedByUserId);
        if (!result.IsSuccess) return Result.Failure<CustomerDto>(result.Error!);
        
        return await GetByIdAsync(id);
    }

    private static CustomerDto MapToDto(Customer c) => new()
    {
        Id = c.Id,
        FullName = c.FullName,
        Phone = c.Phone,
        IdNumber = c.IdNumber,
        Address = c.Address,
        DateOfBirth = DateOnly.FromDateTime(c.DateOfBirth),
        Status = c.Status,
        CreditScore = c.CreditScore,
        CreditStatus = c.CreditStatus,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
