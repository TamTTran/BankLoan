using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Customer;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetListAsync(CustomerFilterRequest filter);
    Task<Result<CustomerDto>> GetByIdAsync(int id);
    Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, int createdByUserId);
    Task<Result<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest request);
    Task<Result> DeleteAsync(int id);
}

public class CustomerService : ICustomerService
{
    private readonly LoanFlowDbContext _db;

    public CustomerService(LoanFlowDbContext db) => _db = db;

    public async Task<PagedResult<CustomerDto>> GetListAsync(CustomerFilterRequest filter)
    {
        var query = _db.Customers.Include(c => c.Branch).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(s) ||
                c.NationalId.Contains(s) ||
                c.PhoneNumber.Contains(s));
        }
        if (filter.BranchId.HasValue)
            query = query.Where(c => c.BranchId == filter.BranchId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                FullName = c.FullName,
                NationalId = c.NationalId,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                DateOfBirth = c.DateOfBirth,
                Address = c.Address,
                Occupation = c.Occupation,
                MonthlyIncome = c.MonthlyIncome,
                BranchName = c.Branch != null ? c.Branch.Name : null,
                CreatedAt = c.CreatedAt,
                ActiveLoans = c.LoanApplications.Count(a => a.Loan != null && a.Loan.Status == "Active"),
                TotalOutstanding = c.LoanApplications
                    .Where(a => a.Loan != null)
                    .Sum(a => a.Loan!.OutstandingBalance)
            })
            .ToListAsync();

        return new PagedResult<CustomerDto>
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<Result<CustomerDto>> GetByIdAsync(int id)
    {
        var c = await _db.Customers.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return Result.Failure<CustomerDto>("Khách hàng không tồn tại.");
        return Result.Success(MapToDto(c));
    }

    public async Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest req, int createdByUserId)
    {
        if (await _db.Customers.AnyAsync(c => c.NationalId == req.NationalId))
            return Result.Failure<CustomerDto>("CCCD/CMND đã tồn tại trong hệ thống.");

        if (await _db.Customers.AnyAsync(c => c.PhoneNumber == req.PhoneNumber))
            return Result.Failure<CustomerDto>("Số điện thoại đã được đăng ký.");

        var code = await GenerateCustomerCodeAsync();
        var entity = new Customer
        {
            CustomerCode = code,
            FullName = req.FullName,
            NationalId = req.NationalId,
            PhoneNumber = req.PhoneNumber,
            Email = req.Email,
            DateOfBirth = req.DateOfBirth,
            Address = req.Address,
            Occupation = req.Occupation,
            MonthlyIncome = req.MonthlyIncome,
            BranchId = req.BranchId,
            CreatedByUserId = createdByUserId,
        };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog { UserId = createdByUserId, Action = "CREATE", EntityType = "Customer", EntityId = entity.Id.ToString(), NewValues = $"{{\"code\":\"{code}\",\"name\":\"{entity.FullName}\"}}" });
        await _db.SaveChangesAsync();

        return await GetByIdAsync(entity.Id);
    }

    public async Task<Result<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest req)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return Result.Failure<CustomerDto>("Khách hàng không tồn tại.");

        if (entity.PhoneNumber != req.PhoneNumber && await _db.Customers.AnyAsync(c => c.PhoneNumber == req.PhoneNumber && c.Id != id))
            return Result.Failure<CustomerDto>("Số điện thoại đã được đăng ký.");

        entity.FullName = req.FullName;
        entity.Email = req.Email;
        entity.Address = req.Address;
        entity.Occupation = req.Occupation;
        entity.MonthlyIncome = req.MonthlyIncome;
        entity.PhoneNumber = req.PhoneNumber;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return Result.Failure("Khách hàng không tồn tại.");

        var hasLoans = await _db.LoanApplications.AnyAsync(a => a.CustomerId == id);
        if (hasLoans) return Result.Failure("Không thể xóa khách hàng đã có hồ sơ vay.");

        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"CUS-{today}-";
        var lastCode = await _db.Customers
            .Where(c => c.CustomerCode.StartsWith(prefix))
            .OrderByDescending(c => c.CustomerCode)
            .Select(c => c.CustomerCode)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (lastCode is not null)
        {
            var parts = lastCode.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var n)) seq = n + 1;
        }
        return $"{prefix}{seq:D4}";
    }

    private static CustomerDto MapToDto(Customer c) => new()
    {
        Id = c.Id, CustomerCode = c.CustomerCode, FullName = c.FullName,
        NationalId = c.NationalId, PhoneNumber = c.PhoneNumber, Email = c.Email,
        DateOfBirth = c.DateOfBirth, Address = c.Address, Occupation = c.Occupation,
        MonthlyIncome = c.MonthlyIncome, BranchName = c.Branch?.Name,
        CreatedAt = c.CreatedAt
    };
}
