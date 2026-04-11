using System.Text.Json;

namespace LoanFlow.Domain.Entities;

public class LoanProduct
{
    public int Id { get; set; }
    public string ProductName { get; set; } = default!;     // unique
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public string AllowedTenorsJson { get; set; } = "[]";   // JSON array e.g. [6,12,18]
    public decimal MonthlyInterestRate { get; set; }        // decimal e.g. 0.015 = 1.5%
    public decimal ProcessingFee { get; set; }              // fixed fee amount
    public string RepaymentFrequency { get; set; } = "Monthly";
    public string Status { get; set; } = "Active";          // Active | Inactive

    // ── Bank-grade product fields ───────────────────────────────────────────

    /// <summary>Product category: PersonalUnsecured | PersonalSecured | Mortgage | AutoLoan | BusinessLoan | Overdraft</summary>
    public string ProductType { get; set; } = "PersonalUnsecured";

    /// <summary>Interest calculation method: Flat | ReducingBalance</summary>
    public string InterestMethod { get; set; } = "Flat";

    /// <summary>Grace period months — during grace period only interest is paid, no principal</summary>
    public int GracePeriodMonths { get; set; } = 0;

    /// <summary>Whether this product requires collateral</summary>
    public bool RequiresCollateral { get; set; } = false;

    /// <summary>Max Loan-to-Value ratio for secured loans (e.g. 0.70 = 70%)</summary>
    public decimal? MaxLtvRatio { get; set; }

    /// <summary>Early settlement penalty rate as % of remaining principal (e.g. 0.02 = 2%)</summary>
    public decimal EarlySettlementFeeRate { get; set; } = 0;

    /// <summary>Late fee rate applied to overdue amount (per annum, e.g. 0.12 = 12%/year → daily = rate/360)</summary>
    public decimal LateFeeRate { get; set; } = 0;

    /// <summary>Number of grace days after due date before late fee calculation starts</summary>
    public int LateFeeGraceDays { get; set; } = 0;

    /// <summary>Promotional interest rate for first N months (nullable = no promo)</summary>
    public decimal? PromotionalRate { get; set; }

    /// <summary>Number of months the promotional rate applies</summary>
    public int PromotionalMonths { get; set; } = 0;

    /// <summary>Product description / marketing text</summary>
    public string? Description { get; set; }

    // ── Existing timestamps & nav ───────────────────────────────────────────

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();

    // Helper: parse allowed tenors from JSON
    public List<int> GetAllowedTenors() =>
        JsonSerializer.Deserialize<List<int>>(AllowedTenorsJson) ?? [];
}
