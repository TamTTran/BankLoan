namespace LoanFlow.Domain.Entities;

/// <summary>
/// Persistent daily snapshot of overdue state per loan.
/// Created by the DailyOverdueBackgroundService.
/// Used for historical DPD/bucket reporting and audit trail.
/// </summary>
public class OverdueDailySnapshot
{
    public long Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly SnapshotDate { get; set; }              // business date (UTC+7)
    public int Dpd { get; set; }                            // days past due
    public string Bucket { get; set; } = default!;          // DPD bucket: Current, DPD1_7, DPD8_30, DPD31Plus
    public decimal OverdueAmount { get; set; }              // total overdue installment amount
    public decimal OutstandingAmount { get; set; }          // loan outstanding at snapshot time
    public decimal LateFeeAccrued { get; set; }             // total pending late fee amount
    public int OverdueInstallments { get; set; }            // count of overdue installments
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
}
