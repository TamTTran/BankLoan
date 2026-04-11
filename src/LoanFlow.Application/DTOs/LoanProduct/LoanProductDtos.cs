namespace LoanFlow.Application.DTOs.LoanProduct;

public class LoanProductDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public List<int> AllowedTenors { get; set; } = [];
    public decimal MonthlyInterestRate { get; set; }
    public decimal ProcessingFee { get; set; }
    public string RepaymentFrequency { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    // Bank-grade fields
    public string ProductType { get; set; } = "PersonalUnsecured";
    public string InterestMethod { get; set; } = "Flat";
    public int GracePeriodMonths { get; set; }
    public bool RequiresCollateral { get; set; }
    public decimal? MaxLtvRatio { get; set; }
    public decimal EarlySettlementFeeRate { get; set; }
    public decimal LateFeeRate { get; set; }
    public int LateFeeGraceDays { get; set; }
    public decimal? PromotionalRate { get; set; }
    public int PromotionalMonths { get; set; }
    public string? Description { get; set; }
}

// POST /loan-products (§17.5)
public class CreateLoanProductRequest
{
    public string ProductName { get; set; } = default!;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public List<int> AllowedTenors { get; set; } = [];
    public decimal MonthlyInterestRate { get; set; }
    public decimal ProcessingFee { get; set; }
    public string RepaymentFrequency { get; set; } = "Monthly";
    public string Status { get; set; } = "Active";

    // Bank-grade fields
    public string ProductType { get; set; } = "PersonalUnsecured";
    public string InterestMethod { get; set; } = "Flat";
    public int GracePeriodMonths { get; set; } = 0;
    public bool RequiresCollateral { get; set; } = false;
    public decimal? MaxLtvRatio { get; set; }
    public decimal EarlySettlementFeeRate { get; set; } = 0;
    public decimal LateFeeRate { get; set; } = 0;
    public int LateFeeGraceDays { get; set; } = 0;
    public decimal? PromotionalRate { get; set; }
    public int PromotionalMonths { get; set; } = 0;
    public string? Description { get; set; }
}

// PATCH /loan-products/{id}
public class UpdateLoanProductRequest
{
    public string ProductName { get; set; } = default!;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public List<int> AllowedTenors { get; set; } = [];
    public decimal MonthlyInterestRate { get; set; }
    public decimal ProcessingFee { get; set; }
    public string Status { get; set; } = default!;

    // Bank-grade fields
    public string ProductType { get; set; } = "PersonalUnsecured";
    public string InterestMethod { get; set; } = "Flat";
    public int GracePeriodMonths { get; set; } = 0;
    public bool RequiresCollateral { get; set; } = false;
    public decimal? MaxLtvRatio { get; set; }
    public decimal EarlySettlementFeeRate { get; set; } = 0;
    public decimal LateFeeRate { get; set; } = 0;
    public int LateFeeGraceDays { get; set; } = 0;
    public decimal? PromotionalRate { get; set; }
    public int PromotionalMonths { get; set; } = 0;
    public string? Description { get; set; }
}

// POST /applications/preview (§17.6)
public class InstallmentPreviewRequest
{
    public int ProductId { get; set; }
    public decimal Amount { get; set; }
    public int TenorMonths { get; set; }
}

public class InstallmentPreviewDto
{
    public decimal LoanAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyInterestRate { get; set; }
    public string InterestMethod { get; set; } = "Flat";
    public decimal MonthlyPrincipal { get; set; }       // for Flat: constant; for RB: first month estimate
    public decimal MonthlyInterest { get; set; }        // for Flat: constant; for RB: first month
    public decimal MonthlyInstallment { get; set; }     // for Flat: constant; for RB: EMI
    public decimal TotalRepayment { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal ProcessingFee { get; set; }
    public int GracePeriodMonths { get; set; }
    public decimal? PromotionalRate { get; set; }
    public int PromotionalMonths { get; set; }
}

