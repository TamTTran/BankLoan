namespace LoanFlow.Domain.Entities;

public class CollectionNote
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly NoteDate { get; set; }
    public string NoteText { get; set; } = default!;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public User CreatedBy { get; set; } = default!;
}
