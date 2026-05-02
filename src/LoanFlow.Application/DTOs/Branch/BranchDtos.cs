using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

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
