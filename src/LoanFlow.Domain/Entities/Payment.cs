namespace LoanFlow.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal Amount { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal FeePaid { get; set; } = 0;
    public decimal PenaltyPaid { get; set; } = 0;
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int CollectedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public User CollectedBy { get; set; } = default!;
}
