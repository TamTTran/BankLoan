namespace LoanFlow.Domain.Entities;

public class LoanDisbursement
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal DisbursedAmount { get; set; }            // must equal approved amount
    public DateOnly DisbursementDate { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public User RecordedBy { get; set; } = default!;
}
