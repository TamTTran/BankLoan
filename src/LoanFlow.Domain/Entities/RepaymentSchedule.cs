namespace LoanFlow.Domain.Entities;

public class RepaymentSchedule
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int InstallmentNo { get; set; }                  // 1, 2, 3 … N
    public DateOnly DueDate { get; set; }
    public decimal PrincipalDue { get; set; }
    public decimal InterestDue { get; set; }
    public decimal PrincipalPaid { get; set; } = 0;
    public decimal InterestPaid { get; set; } = 0;
    public decimal RemainingDueAmount { get; set; }         // = (PrincipalDue - PrincipalPaid) + (InterestDue - InterestPaid)
    public string Status { get; set; } = "Unpaid";         // Unpaid | Partial | Paid | Overdue
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[]? RowVersion { get; set; }

    public Loan Loan { get; set; } = default!;
}
