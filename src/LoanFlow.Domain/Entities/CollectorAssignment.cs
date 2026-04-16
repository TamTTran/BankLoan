namespace LoanFlow.Domain.Entities;

/// <summary>
/// Tracks which collector is responsible for which overdue loan.
/// Supports transfer and completion workflows.
/// </summary>
public class CollectorAssignment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int CollectorUserId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }

    /// <summary>Active | Completed | Transferred</summary>
    public string Status { get; set; } = Constants.CollectorAssignmentStatus.Active;

    /// <summary>High | Medium | Low — derived from DPD bucket at assignment time.</summary>
    public string Priority { get; set; } = Constants.CollectionPriority.Medium;

    // Navigation
    public Loan Loan { get; set; } = default!;
    public User Collector { get; set; } = default!;
}
