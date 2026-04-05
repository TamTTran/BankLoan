namespace LoanFlow.Application.DTOs.LoanApplication;

public class LoanApplicationDto
{
    public int Id { get; set; }
    public string ApplicationNumber { get; set; } = default!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    public int LoanProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal RequestedAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal EstimatedInstallment { get; set; }
    public string Purpose { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public List<DecisionHistoryDto> DecisionHistory { get; set; } = [];
}

public class DecisionHistoryDto
{
    public string Decision { get; set; } = default!;
    public string? Remark { get; set; }
    public string DecidedBy { get; set; } = default!;
    public DateTime DecidedAt { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public int? ApprovedTenorMonths { get; set; }
}

public class CreateApplicationRequest
{
    public int CustomerId { get; set; }
    public int LoanProductId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TenorMonths { get; set; }
    public string Purpose { get; set; } = default!;
    public string? Notes { get; set; }
}

public class ApprovalDecisionRequest
{
    public string Decision { get; set; } = default!; // Approved | Rejected | RequestMoreInfo
    public string? Remark { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public int? ApprovedTenorMonths { get; set; }
}

public class ApplicationFilterRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? BranchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
