namespace LoanFlow.Domain.Entities;

/// <summary>
/// Tracks reversal/adjustment requests with 2-step approval flow.
/// Original transaction is preserved; a compensating transaction is created.
/// </summary>
public class TransactionReversal
{
    public int Id { get; set; }

    /// <summary>The original LoanTransaction that is being reversed.</summary>
    public int OriginalTransactionId { get; set; }

    /// <summary>The compensating LoanTransaction created by the reversal (set after approval).</summary>
    public int? ReversalTransactionId { get; set; }

    public int LoanId { get; set; }

    /// <summary>PaymentReversal | DisbursementCorrection | FeeAdjustment</summary>
    public string ReversalType { get; set; } = default!;

    public decimal OriginalAmount { get; set; }
    public decimal ReversedAmount { get; set; }

    /// <summary>Business reason for the reversal — required.</summary>
    public string Reason { get; set; } = default!;

    /// <summary>Pending | Approved | Rejected</summary>
    public string Status { get; set; } = Constants.ReversalStatus.Pending;

    public int RequestedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    // Navigation
    public Loan Loan { get; set; } = default!;
    public LoanTransaction OriginalTransaction { get; set; } = default!;
    public LoanTransaction? ReversalTransaction { get; set; }
    public User RequestedBy { get; set; } = default!;
    public User? ApprovedBy { get; set; }
}
