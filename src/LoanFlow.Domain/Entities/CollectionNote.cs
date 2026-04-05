namespace LoanFlow.Domain.Entities;

public class CollectionNote
{
    public int Id { get; set; }
    public int DelinquencyStatusId { get; set; }
    public int LoanId { get; set; }
    public string Note { get; set; } = default!;
    public string? ContactResult { get; set; } // Contacted | NoAnswer | Promised | Refused
    public DateTime? PromisedPaymentDate { get; set; }
    public decimal? PromisedAmount { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DelinquencyStatus DelinquencyStatus { get; set; } = default!;
    public User CreatedBy { get; set; } = default!;
}
