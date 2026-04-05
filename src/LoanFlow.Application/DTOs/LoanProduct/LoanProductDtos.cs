namespace LoanFlow.Application.DTOs.LoanProduct;

public class LoanProductDto
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTenorMonths { get; set; }
    public int MaxTenorMonths { get; set; }
    public decimal InterestRatePerMonth { get; set; }
    public decimal? ProcessingFeeRate { get; set; }
    public string RepaymentFrequency { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateLoanProductRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTenorMonths { get; set; }
    public int MaxTenorMonths { get; set; }
    public decimal InterestRatePerMonth { get; set; }
    public decimal? ProcessingFeeRate { get; set; }
    public string RepaymentFrequency { get; set; } = "Monthly";
}

public class UpdateLoanProductRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTenorMonths { get; set; }
    public int MaxTenorMonths { get; set; }
    public decimal InterestRatePerMonth { get; set; }
    public decimal? ProcessingFeeRate { get; set; }
    public bool IsActive { get; set; }
}

public class InstallmentPreviewRequest
{
    public int LoanProductId { get; set; }
    public decimal Amount { get; set; }
    public int TenorMonths { get; set; }
}

public class InstallmentPreviewDto
{
    public decimal LoanAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal InterestRatePerMonth { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal TotalRepayment { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal ProcessingFee { get; set; }
}
