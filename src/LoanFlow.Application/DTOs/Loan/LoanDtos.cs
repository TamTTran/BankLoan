namespace LoanFlow.Application.DTOs.Loan;

public class LoanDto
{
    public int Id { get; set; }
    public string LoanNumber { get; set; } = default!;
    public string ApplicationNumber { get; set; } = default!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRatePerMonth { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal TotalPaid { get; set; }
    public string Status { get; set; } = default!;
    public DateTime DisbursedAt { get; set; }
    public DateTime MaturityDate { get; set; }
    public List<RepaymentScheduleDto> Schedule { get; set; } = [];
    public List<PaymentSummaryDto> RecentPayments { get; set; } = [];
}

public class RepaymentScheduleDto
{
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalDue { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = default!;
    public DateTime? PaidAt { get; set; }
}

public class PaymentSummaryDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string CollectedBy { get; set; } = default!;
}

public class CreateLoanRequest
{
    public int LoanApplicationId { get; set; }
    public DateTime DisbursedAt { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class CollectPaymentRequest
{
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class LoanFilterRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? CustomerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
