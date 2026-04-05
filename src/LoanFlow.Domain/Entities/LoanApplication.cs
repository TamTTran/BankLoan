namespace LoanFlow.Domain.Entities;

public class LoanApplication
{
    public int Id { get; set; }
    public string ApplicationNumber { get; set; } = default!; // APP-YYYYMMDD-XXXXX
    public int CustomerId { get; set; }
    public int LoanProductId { get; set; }
    public int? BranchId { get; set; }
    public int CreatedByUserId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal EstimatedInstallment { get; set; }
    public string Purpose { get; set; } = default!;
    public string Status { get; set; } = "Draft"; // Draft | Submitted | UnderReview | Approved | Rejected | Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Notes { get; set; }

    public Customer Customer { get; set; } = default!;
    public LoanProduct LoanProduct { get; set; } = default!;
    public Branch? Branch { get; set; }
    public User CreatedBy { get; set; } = default!;
    public ICollection<ApplicationDecision> Decisions { get; set; } = new List<ApplicationDecision>();
    public Loan? Loan { get; set; }
}
