namespace LoanFlow.Domain.Entities;

public class JournalEntry
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = default!; // e.g., "DISB-1001", "PAY-2035"
    public DateOnly BusinessDate { get; set; }
    public string Description { get; set; } = default!;
    
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}
