namespace LoanFlow.Domain.Entities;

public class LoanDisbursement
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DisbursedAt { get; set; }
    public string PaymentMethod { get; set; } = default!; // Cash | BankTransfer | Cheque
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int ProcessedByUserId { get; set; }

    public Loan Loan { get; set; } = default!;
    public User ProcessedBy { get; set; } = default!;
}
