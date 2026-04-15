namespace LoanFlow.Domain.Entities;

/// <summary>
/// Records mismatches found during daily reconciliation.
/// Exceptions remain Open until manually resolved or acknowledged.
/// </summary>
public class ReconciliationException
{
    public int Id { get; set; }
    public DateOnly BusinessDate { get; set; }

    /// <summary>PaymentMismatch | DisbursementMismatch | OutstandingMismatch</summary>
    public string ExceptionType { get; set; } = default!;

    public int? LoanId { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Difference { get; set; }

    public string? Description { get; set; }

    /// <summary>Open | Resolved | Acknowledged</summary>
    public string Status { get; set; } = Constants.ReconciliationStatus.Open;

    public int? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Loan? Loan { get; set; }
    public User? ResolvedBy { get; set; }
}
