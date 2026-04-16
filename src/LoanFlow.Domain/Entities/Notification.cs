namespace LoanFlow.Domain.Entities;

/// <summary>
/// System notifications (reminders, alerts) with optional deeplink support.
/// </summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? LoanId { get; set; }
    public int? CustomerId { get; set; }

    /// <summary>DueReminder | OverdueReminder | ApprovalPending | System</summary>
    public string Type { get; set; } = default!;
    
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    
    public string? DeeplinkUrl { get; set; }
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public User User { get; set; } = default!;
}
