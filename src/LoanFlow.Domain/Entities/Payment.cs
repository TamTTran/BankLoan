namespace LoanFlow.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;   // Cash | BankTransfer
    public string? Remarks { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public User CreatedBy { get; set; } = default!;
}
