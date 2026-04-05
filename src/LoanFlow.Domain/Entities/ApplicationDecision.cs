namespace LoanFlow.Domain.Entities;

public class ApplicationDecision
{
    public int Id { get; set; }
    public int LoanApplicationId { get; set; }
    public int DecidedByUserId { get; set; }
    public string Decision { get; set; } = default!; // Approved | Rejected | RequestMoreInfo
    public string? Remark { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    public decimal? ApprovedAmount { get; set; }
    public int? ApprovedTenorMonths { get; set; }

    public LoanApplication LoanApplication { get; set; } = default!;
    public User DecidedBy { get; set; } = default!;
}
