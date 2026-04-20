namespace LoanFlow.Domain.Entities;

/// <summary>
/// Records exactly how each Payment amount is split and allocated.
/// Provides the ledger chain: Payment → PaymentAllocation → RepaymentSchedule.
/// AllocationType: Principal | Interest | LateFee | Suspense
/// </summary>
public class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }                    // FK → Payments
    public int LoanTransactionId { get; set; }            // FK → LoanTransactions
    public int? ScheduleId { get; set; }                  // FK → RepaymentSchedules (null for Suspense)
    public int? LateFeeId { get; set; }                   // FK → LateFees (for LateFee type)

    /// <summary>Principal | Interest | LateFee | Suspense</summary>
    public string AllocationType { get; set; } = default!;

    public decimal Amount { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Payment Payment { get; set; } = default!;
    public LoanTransaction LoanTransaction { get; set; } = default!;
    public RepaymentSchedule? Schedule { get; set; }
    public LateFee? LateFeeRef { get; set; }
}
