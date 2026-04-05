namespace LoanFlow.Domain.Entities;

public class Loan
{
    public int Id { get; set; }
    public string LoanNumber { get; set; } = default!; // LN-YYYYMMDD-XXXXX
    public int LoanApplicationId { get; set; }
    public int CustomerId { get; set; }
    public int LoanProductId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRatePerMonth { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal TotalPaid { get; set; } = 0;
    public string Status { get; set; } = "Active"; // Active | Closed | Defaulted | WrittenOff
    public DateTime DisbursedAt { get; set; }
    public DateTime MaturityDate { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public LoanApplication LoanApplication { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
    public LoanProduct LoanProduct { get; set; } = default!;
    public User CreatedBy { get; set; } = default!;
    public LoanDisbursement? Disbursement { get; set; }
    public ICollection<RepaymentSchedule> RepaymentSchedules { get; set; } = new List<RepaymentSchedule>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public DelinquencyStatus? DelinquencyStatus { get; set; }
}
