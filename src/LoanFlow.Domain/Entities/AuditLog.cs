namespace LoanFlow.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = default!;     // CREATE | UPDATE | DELETE | LOGIN | APPROVE | REJECT
    public string EntityType { get; set; } = default!; // Customer | Loan | Application | Payment
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }              // JSON snapshot
    public string? NewValues { get; set; }              // JSON snapshot
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
