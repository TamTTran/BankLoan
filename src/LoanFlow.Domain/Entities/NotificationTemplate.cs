namespace LoanFlow.Domain.Entities;

/// <summary>
/// Notification template for automated message generation.
/// Supports variable substitution: {LoanAccountNo}, {Amount}, {DueDate}, etc.
/// </summary>
public class NotificationTemplate
{
    public int Id { get; set; }

    /// <summary>DueReminder | OverdueDay1 | OverdueDay7 | OverdueDay30 | WriteOffWarning | System</summary>
    public string TemplateCode { get; set; } = default!;
    
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;         // Supports {placeholders}

    /// <summary>SMS | Email | InApp | All</summary>
    public string Channel { get; set; } = "InApp";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
