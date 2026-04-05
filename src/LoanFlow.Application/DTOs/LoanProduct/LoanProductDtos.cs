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
    public decimal MonthlyPrincipal { get; set; }
    public decimal MonthlyInterest { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal TotalRepayment { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal ProcessingFee { get; set; }
}
