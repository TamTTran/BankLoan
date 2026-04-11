namespace LoanFlow.Domain.Entities;

public class LoanApplication
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int RequestedTenorMonths { get; set; }
    public string? Purpose { get; set; }
    public string? Remarks { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Submitted | Approved | Rejected
    public int CreatedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }

    // ── Multi-level approval tracking ───────────────────────────────────────
    public int? CurrentApprovalLevel { get; set; }          // current level reached (1, 2, 3…)
    public int? RequiredApprovalLevel { get; set; }         // total levels needed (determined at submit time)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = default!;
    public LoanProduct Product { get; set; } = default!;
    public User CreatedBy { get; set; } = default!;
    public ICollection<ApplicationDecision> Decisions { get; set; } = new List<ApplicationDecision>();
    public Loan? Loan { get; set; }
}
