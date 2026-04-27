namespace LoanFlow.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public int? ActorUserId { get; set; }
    public string ActionCode { get; set; } = default!;      // standardized e.g. CUSTOMER_CREATED
    public string EntityType { get; set; } = default!;      // Customer | Application | Loan | Payment
    public int EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    
    // ── Nâng cao (Module 6) ────────────────────────────────────────────────
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? ActorUser { get; set; }
}
