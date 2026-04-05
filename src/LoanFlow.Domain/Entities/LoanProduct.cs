namespace LoanFlow.Domain.Entities;

public class LoanProduct
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTenorMonths { get; set; }
    public int MaxTenorMonths { get; set; }
    public decimal InterestRatePerMonth { get; set; } // % per month
    public decimal? ProcessingFeeRate { get; set; } // % of loan amount
    public string RepaymentFrequency { get; set; } = "Monthly"; // Monthly | Weekly | BiWeekly
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User? CreatedBy { get; set; }
    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
}
