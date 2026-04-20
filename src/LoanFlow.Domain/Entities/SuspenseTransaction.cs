namespace LoanFlow.Domain.Entities;

/// <summary>
/// Tracks overpayment amounts that exceed the current total due.
/// These funds are held in suspense and can be applied to future installments or refunded.
/// Ledger chain: Payment → SuspenseTransaction (via SourcePaymentId)
/// </summary>
public class SuspenseTransaction
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal Amount { get; set; }
    
    public string Reason { get; set; } = "Overpayment";
    
    /// <summary>Pending | Applied | Refunded</summary>
    public string Status { get; set; } = Constants.SuspenseStatus.Pending;
    
    // ── Ledger chain: trace back to the originating payment ─────────────
    /// <summary>FK to the Payment that created this suspense (for audit trail).</summary>
    public int? SourcePaymentId { get; set; }
    /// <summary>FK to the LoanTransaction entry for this suspense posting.</summary>
    public int? SourceLoanTransactionId { get; set; }

    // When applied to future installments
    public DateOnly? AppliedAt { get; set; }
    public int? AppliedByUserId { get; set; }
    public int? AppliedToRepaymentScheduleId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Loan Loan { get; set; } = default!;
    public Payment? SourcePayment { get; set; }
    public LoanTransaction? SourceLoanTransaction { get; set; }
    public RepaymentSchedule? AppliedToSchedule { get; set; }
}

