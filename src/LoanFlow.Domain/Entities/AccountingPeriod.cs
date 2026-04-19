namespace LoanFlow.Domain.Entities;

/// <summary>
/// Represents an accounting period (monthly).
/// Controls posting lock: journal entries cannot be posted into a closed period.
/// </summary>
public class AccountingPeriod
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    
    /// <summary>Open | Closing | Closed</summary>
    public string Status { get; set; } = "Open";
    
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? Notes { get; set; }

    public User? ClosedBy { get; set; }
}
