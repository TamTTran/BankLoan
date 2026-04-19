namespace LoanFlow.Domain.Entities;

/// <summary>
/// Dunning strategy configuration per overdue bucket.
/// Defines escalation actions at specific DPD thresholds.
/// </summary>
public class DunningStrategy
{
    public int Id { get; set; }

    /// <summary>DPD threshold to trigger this step (e.g., 1, 7, 30, 60, 90)</summary>
    public int DpdThreshold { get; set; }

    /// <summary>Action: SendSms | SendEmail | AssignCollector | EscalateManager | LegalNotice</summary>
    public string ActionType { get; set; } = default!;

    public int? NotificationTemplateId { get; set; }
    public string? Description { get; set; }
    public int Priority { get; set; } = 0;              // Lower = higher priority
    public bool IsActive { get; set; } = true;

    public NotificationTemplate? Template { get; set; }
}
