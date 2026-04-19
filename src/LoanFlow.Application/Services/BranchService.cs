using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class BranchDto
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public int? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public string Status { get; set; } = "";
}

public class CreateBranchRequest
{
    public string BranchCode { get; set; } = default!;
    public string BranchName { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public int? ManagerUserId { get; set; }
}

public class UpdateBranchRequest
{
    public string? BranchName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public int? ManagerUserId { get; set; }
    public string? Status { get; set; }
}

public class BranchPortfolioDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int TotalCustomers { get; set; }
    public int TotalLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int OverdueLoans { get; set; }
    public int TotalUsers { get; set; }
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IBranchService
{
    Task<List<BranchDto>> GetAllAsync();
    Task<Result<BranchDto>> CreateAsync(CreateBranchRequest req);
    Task<Result> UpdateAsync(int id, UpdateBranchRequest req);
    Task<BranchPortfolioDto?> GetBranchPortfolioAsync(int branchId);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class BranchService : IBranchService
{
    private readonly LoanFlowDbContext _db;

    public BranchService(LoanFlowDbContext db) => _db = db;

    public async Task<List<BranchDto>> GetAllAsync()
    {
        return await _db.Set<Branch>()
            .Include(b => b.Manager)
            .Select(b => new BranchDto
            {
                Id = b.Id, BranchCode = b.BranchCode, BranchName = b.BranchName,
                Address = b.Address, Phone = b.Phone,
                ManagerUserId = b.ManagerUserId,
                ManagerName = b.Manager != null ? b.Manager.FullName : null,
                Status = b.Status
            })
            .ToListAsync();
    }

    public async Task<Result<BranchDto>> CreateAsync(CreateBranchRequest req)
    {
        if (await _db.Set<Branch>().AnyAsync(b => b.BranchCode == req.BranchCode))
            return Result.Failure<BranchDto>($"Branch code '{req.BranchCode}' already exists.");

        var branch = new Branch
        {
            BranchCode = req.BranchCode,
            BranchName = req.BranchName,
            Address = req.Address,
            Phone = req.Phone,
            ManagerUserId = req.ManagerUserId
        };

        _db.Set<Branch>().Add(branch);
        await _db.SaveChangesAsync();

        return Result.Success(new BranchDto
        {
            Id = branch.Id, BranchCode = branch.BranchCode, BranchName = branch.BranchName,
            Address = branch.Address, Phone = branch.Phone,
            ManagerUserId = branch.ManagerUserId, Status = branch.Status
        });
    }

    public async Task<Result> UpdateAsync(int id, UpdateBranchRequest req)
    {
        var branch = await _db.Set<Branch>().FindAsync(id);
        if (branch == null) return Result.Failure("Branch not found.");

        if (req.BranchName != null) branch.BranchName = req.BranchName;
        if (req.Address != null) branch.Address = req.Address;
        if (req.Phone != null) branch.Phone = req.Phone;
        if (req.ManagerUserId.HasValue) branch.ManagerUserId = req.ManagerUserId;
        if (req.Status != null) branch.Status = req.Status;
        branch.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<BranchPortfolioDto?> GetBranchPortfolioAsync(int branchId)
    {
        var branch = await _db.Set<Branch>().FindAsync(branchId);
        if (branch == null) return null;

        return new BranchPortfolioDto
        {
            BranchId = branch.Id,
            BranchName = branch.BranchName,
            TotalCustomers = await _db.Customers.CountAsync(c => c.BranchId == branchId),
            TotalLoans = await _db.Loans.CountAsync(l => l.BranchId == branchId),
            TotalOutstanding = await _db.Loans.Where(l => l.BranchId == branchId && l.OutstandingAmount > 0).SumAsync(l => l.OutstandingAmount),
            OverdueLoans = await _db.Loans.CountAsync(l => l.BranchId == branchId && l.Status == "Overdue"),
            TotalUsers = await _db.Users.CountAsync(u => u.BranchId == branchId)
        };
    }
}
