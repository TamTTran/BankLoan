namespace LoanFlow.Domain.Entities;

public class Loan
{
    public int Id { get; set; }
    public string LoanAccountNo { get; set; } = default!;   // unique — e.g. LN-2026-0001
    public int ApplicationId { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyInterestRate { get; set; }        // snapshot from product
    public decimal ProcessingFee { get; set; }              // snapshot from product
    public DateOnly? DisbursementDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public string Status { get; set; } = "PendingDisbursement"; // PendingDisbursement | Active | Overdue | Closed
    public decimal OutstandingAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LoanApplication Application { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
    public LoanProduct Product { get; set; } = default!;
    public LoanDisbursement? Disbursement { get; set; }
    public ICollection<RepaymentSchedule> RepaymentSchedules { get; set; } = new List<RepaymentSchedule>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<LoanTransaction> Transactions { get; set; } = new List<LoanTransaction>();
    public ICollection<CollectionNote> CollectionNotes { get; set; } = new List<CollectionNote>();
}
