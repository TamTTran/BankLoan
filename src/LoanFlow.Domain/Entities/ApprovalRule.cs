namespace LoanFlow.Domain.Entities;

/// <summary>
/// Defines multi-level approval rules based on loan amount thresholds.
/// Mirrors Vietnamese bank practice:
///   - Small loans (&lt;100M): 1 level (Branch approver)
///   - Medium loans (100-500M): 2 levels (Branch + Region)
///   - Large loans (&gt;500M): 3 levels (Branch + Region + HQ)
/// </summary>
public class ApprovalRule
{
    public int Id { get; set; }
    public string? ProductType { get; set; }            // null = all products
    public decimal MinAmount { get; set; }              // from this amount
    public decimal MaxAmount { get; set; }              // up to this amount
    public int RequiredLevel { get; set; }              // 1, 2, or 3
    public string ApproverRole { get; set; } = default!; // required role for this level
    public int? MinCicScore { get; set; }               // required CIC score to auto-approve
    public bool IsAutoApprove { get; set; } = false;    // triggers Straight-Through Processing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
