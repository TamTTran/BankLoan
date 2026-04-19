namespace LoanFlow.Domain.Entities;

/// <summary>
/// Records a collateral revaluation event.
/// </summary>
public class CollateralRevaluation
{
    public int Id { get; set; }
    public int CollateralId { get; set; }
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public string? RevaluedBy { get; set; }          // assessor name or firm
    public string? Reason { get; set; }
    public DateTime RevaluationDate { get; set; } = DateTime.UtcNow;
    public int RecordedByUserId { get; set; }

    public Collateral Collateral { get; set; } = default!;
    public User RecordedBy { get; set; } = default!;
}
