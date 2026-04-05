namespace LoanFlow.Domain.Entities;

public class DelinquencyStatus
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int DaysOverdue { get; set; }
    public string Bucket { get; set; } = default!; // Current | DPD1_7 | DPD8_30 | DPD31_90 | DPD90Plus
    public decimal OverdueAmount { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
    public string? CollectorNote { get; set; }
    public int? AssignedCollectorId { get; set; }

    public Loan Loan { get; set; } = default!;
    public User? AssignedCollector { get; set; }
    public ICollection<CollectionNote> CollectionNotes { get; set; } = new List<CollectionNote>();
}
