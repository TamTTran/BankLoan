namespace LoanFlow.Domain.Entities;

public class RepaymentSchedule
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int InstallmentNumber { get; set; } // 1, 2, 3 ... N
    public DateTime DueDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalDue { get; set; }
    public decimal PaidAmount { get; set; } = 0;
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | PartiallyPaid | Paid | Overdue
    public DateTime? PaidAt { get; set; }

    public Loan Loan { get; set; } = default!;
}
