using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class ReversalRequestDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string LoanAccountNo { get; set; } = "";
    public int OriginalTransactionId { get; set; }
    public string ReversalType { get; set; } = "";
    public decimal OriginalAmount { get; set; }
    public decimal ReversedAmount { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class RequestReversalDto
{
    public int PaymentId { get; set; }
    public string Reason { get; set; } = "";
}

public class ApproveReversalDto
{
    public string? Notes { get; set; }
}

public class AdjustFeeDto
{
    public int LateFeeId { get; set; }
    public decimal NewAmount { get; set; }
    public string Reason { get; set; } = "";
}
