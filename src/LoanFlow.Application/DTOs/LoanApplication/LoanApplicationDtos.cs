namespace LoanFlow.Application.DTOs.LoanApplication;

public class LoanApplicationDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal RequestedAmount { get; set; }
    public int RequestedTenorMonths { get; set; }
    public string? Purpose { get; set; }
    public string? Remarks { get; set; }
    public string Status { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<DecisionHistoryDto> DecisionHistory { get; set; } = [];
    public int? LoanId { get; set; }  // set if loan has been created
}

public class DecisionHistoryDto
{
    public string Decision { get; set; } = default!;
    public string? DecisionReason { get; set; }
    public string DecidedBy { get; set; } = default!;
    public DateTime DecidedAt { get; set; }
}

// POST /applications
public class CreateApplicationRequest
{
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int RequestedTenorMonths { get; set; }
    public string? Purpose { get; set; }
    public string? Remarks { get; set; }
}

// PATCH /applications/{id}
public class UpdateApplicationRequest
{
    public decimal RequestedAmount { get; set; }
    public int RequestedTenorMonths { get; set; }
    public string? Purpose { get; set; }
    public string? Remarks { get; set; }
}

// POST /applications/{id}/reject
public class RejectApplicationRequest
{
    public string Reason { get; set; } = default!;
}

public class ApplicationFilterRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
