namespace LoanFlow.Domain.Entities;

public class JournalEntry
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = default!; // e.g., "DISB-1001", "PAY-2035"
    public DateOnly BusinessDate { get; set; }
    public string Description { get; set; } = default!;
    
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    
    // ── Nâng cấp Core Banking (Module 7) ─────────────────────────────────────
    public string SyncStatus { get; set; } = "Pending"; // Pending | Synced | Failed
    public DateTime? SyncedAt { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}
