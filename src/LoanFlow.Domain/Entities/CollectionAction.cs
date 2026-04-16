namespace LoanFlow.Domain.Entities;

/// <summary>
/// Structured collection action log — replaces freeform notes with
/// actionable tracking: outcome, follow-up dates, and promise-to-pay.
/// </summary>
public class CollectionAction
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int CollectorUserId { get; set; }

    /// <summary>Call | Visit | SMS | Letter | Legal</summary>
    public string ActionType { get; set; } = default!;

    public DateOnly ActionDate { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }

    /// <summary>Contacted | NoAnswer | PromiseToPay | Refused | Escalated</summary>
    public string Outcome { get; set; } = default!;

    // ── Promise-to-Pay fields ──
    public DateOnly? PromiseDate { get; set; }
    public decimal? PromiseAmount { get; set; }

    /// <summary>Pending | Kept | Broken (null if no promise)</summary>
    public string? PromiseStatus { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Loan Loan { get; set; } = default!;
    public User Collector { get; set; } = default!;
}
