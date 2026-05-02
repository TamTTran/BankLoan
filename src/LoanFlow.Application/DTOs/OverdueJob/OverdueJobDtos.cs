using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class OverdueJobResultDto
{
    public DateOnly BusinessDate { get; set; }
    public int TotalLoansProcessed { get; set; }
    public int LoansMarkedOverdue { get; set; }
    public int LoansRevertedToActive { get; set; }
    public int SnapshotsCreated { get; set; }
    public int SnapshotsUpdated { get; set; }
    public int LateFeesCalculated { get; set; }
    public int Errors { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SnapshotSummaryDto
{
    public DateOnly SnapshotDate { get; set; }
    public int TotalOverdueLoans { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public decimal TotalLateFeeAccrued { get; set; }
    public List<BucketSummaryItem> BucketBreakdown { get; set; } = [];
}

public class BucketSummaryItem
{
    public string Bucket { get; set; } = default!;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
