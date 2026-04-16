namespace LoanFlow.Domain.Entities;

/// <summary>
/// Tracks overpayment amounts that exceed the current total due.
/// These funds are held in suspense and can be applied to future installments or refunded.
/// </summary>
public class SuspenseTransaction
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal Amount { get; set; }
    
    public string Reason { get; set; } = "Overpayment";
    
    /// <summary>Pending | Applied | Refunded</summary>
    public string Status { get; set; } = Constants.SuspenseStatus.Pending;
    
    // When applied to future installments
    public DateOnly? AppliedAt { get; set; }
    public int? AppliedByUserId { get; set; }
    public int? AppliedToRepaymentScheduleId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Loan Loan { get; set; } = default!;
    public RepaymentSchedule? AppliedToSchedule { get; set; }
}
