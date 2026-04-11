namespace LoanFlow.Domain.Entities;

/// <summary>
/// Tracks late payment penalties per overdue installment.
/// Formula: fee = overdue_amount × rate × max(0, days_overdue - grace_days) / 360
/// Ref: BIDV charges 150% of loan rate; VPBank uses overdue_amount × late_rate × days / 360.
/// </summary>
public class LateFee
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int ScheduleId { get; set; }               // which installment incurred this fee
    public decimal OverdueAmount { get; set; }         // overdue amount at calculation time
    public int DaysOverdue { get; set; }               // number of days overdue
    public decimal FeeRate { get; set; }               // annual rate from product snapshot
    public decimal FeeAmount { get; set; }             // = overdue_amount × rate × effective_days / 360
    public string Status { get; set; } = "Pending";    // Pending | Paid | Waived
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public RepaymentSchedule Schedule { get; set; } = default!;
}
