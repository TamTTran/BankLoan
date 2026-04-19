namespace LoanFlow.Domain.Entities;

/// <summary>
/// Represents a top-up (additional disbursement) on an existing active loan.
/// </summary>
public class LoanTopUp
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal TopUpAmount { get; set; }
    public decimal PreviousOutstanding { get; set; }
    public decimal NewOutstanding { get; set; }
    public int NewTenorMonths { get; set; }
    public decimal NewMonthlyRate { get; set; }

    /// <summary>Pending | Approved | Rejected | Disbursed</summary>
    public string Status { get; set; } = "Pending";

    public int RequestedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public string? Remarks { get; set; }

    public Loan Loan { get; set; } = default!;
    public User RequestedBy { get; set; } = default!;
    public User? ApprovedBy { get; set; }
}
