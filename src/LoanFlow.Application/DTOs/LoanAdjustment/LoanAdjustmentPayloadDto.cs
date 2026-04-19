namespace LoanFlow.Application.DTOs.LoanAdjustment;

public class LoanAdjustmentPayloadDto
{
    public int LoanId { get; set; }
    public string AdjustmentType { get; set; } = default!; // "Principal", "Interest", "LateFee", "Fee"
    public decimal Amount { get; set; }
    public string ReasonCode { get; set; } = default!;
    public string? Note { get; set; }
}
