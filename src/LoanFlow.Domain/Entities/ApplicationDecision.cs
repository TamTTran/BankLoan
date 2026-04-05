namespace LoanFlow.Domain.Entities;

public class ApplicationDecision
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string Decision { get; set; } = default!;        // Approved | Rejected
    public string? DecisionReason { get; set; }             // required if Rejected
    public int DecidedByUserId { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

    public LoanApplication Application { get; set; } = default!;
    public User DecidedBy { get; set; } = default!;
}
