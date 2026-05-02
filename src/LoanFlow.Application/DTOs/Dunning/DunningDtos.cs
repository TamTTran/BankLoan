namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class DunningStrategyDto
{
    public int Id { get; set; }
    public int DpdThreshold { get; set; }
    public string ActionType { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class NotificationTemplateDto
{
    public int Id { get; set; }
    public string TemplateCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Channel { get; set; } = "";
    public bool IsActive { get; set; }
}
