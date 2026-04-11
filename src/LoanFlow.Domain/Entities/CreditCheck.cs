namespace LoanFlow.Domain.Entities;

/// <summary>
/// Internal credit scoring result for a customer.
/// Score formula (simplified):
///   Start = 100
///   - Each current overdue loan: -20
///   - Each active loan: -5
///   - MaxDPD > 30 (12 months): -30
///   - MaxDPD > 7 (12 months): -15
///   - Outstanding > 80% total borrowed: -10
/// Grade: A(80-100) B(60-79) C(40-59) D(20-39) E(0-19)
/// </summary>
public class CreditCheck
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? ApplicationId { get; set; }            // optional: linked to which application

    // ── Loan history snapshot ───────────────────────────────────────────────
    public int TotalLoans { get; set; }                // lifetime total
    public int ActiveLoans { get; set; }               // currently active
    public int OverdueLoans { get; set; }              // currently overdue
    public int ClosedLoans { get; set; }               // fully paid
    public decimal TotalOutstanding { get; set; }       // sum of all active outstanding
    public int MaxDpdLast12Months { get; set; }         // worst DPD in last 12 months

    // ── Score result ────────────────────────────────────────────────────────
    public int CreditScore { get; set; }               // 0-100
    public string CreditGrade { get; set; } = default!; // A, B, C, D, E
    public string Recommendation { get; set; } = default!; // Approve, Review, Reject

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public int CheckedByUserId { get; set; }

    public Customer Customer { get; set; } = default!;
    public User CheckedBy { get; set; } = default!;
}
