namespace LoanFlow.Domain.Entities;

/// <summary>
/// Mirrors v2 §15.10 loan_transactions — records DISBURSEMENT and PAYMENT events.
/// </summary>
public class LoanTransaction
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int? PaymentId { get; set; }                     // nullable for non-payment events
    public string TransactionType { get; set; } = default!; // Disbursement | Payment
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public Payment? Payment { get; set; }
}
