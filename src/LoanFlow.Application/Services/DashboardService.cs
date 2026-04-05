using LoanFlow.Application.DTOs.Dashboard;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<OverdueBucketReportDto> GetOverdueBucketReportAsync();
}

public class DashboardService : IDashboardService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILoanService _loanService;

    public DashboardService(LoanFlowDbContext db, ILoanService loanService)
    {
        _db = db;
        _loanService = loanService;
    }

    /// <summary>
    /// GET /dashboard/summary — RPT-001
    /// Returns: active loans count, overdue loans count, due today amount, outstanding amount
    /// </summary>
    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        // Use query-time overdue list to get accurate counts (§12.8)
        var overdueList = (await _loanService.GetOverdueListAsync()).ToList();

        var totalActiveLoans = await _db.Loans.CountAsync(l =>
            l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue);

        var totalOverdueLoans = overdueList.Count;

        var totalOutstanding = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .SumAsync(l => l.OutstandingAmount);

        // Due today: installments whose DueDate is today and not yet fully paid
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")));

        var dueTodayAmount = await _db.RepaymentSchedules
            .Where(s => s.DueDate == today && s.Status != InstallmentStatus.Paid)
            .SumAsync(s => s.RemainingDueAmount);

        var pendingApprovals = await _db.LoanApplications
            .CountAsync(a => a.Status == ApplicationStatus.Submitted);

        var statusBreakdown = await _db.Loans
            .GroupBy(l => l.Status)
            .Select(g => new LoanStatusSummaryDto
            {
                Status = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(l => l.OutstandingAmount)
            })
            .ToListAsync();

        return new DashboardSummaryDto
        {
            TotalActiveLoans = totalActiveLoans,
            TotalOverdueLoans = totalOverdueLoans,
            DueTodayAmount = dueTodayAmount,
            TotalOutstandingAmount = totalOutstanding,
            PendingApprovals = pendingApprovals,
            LoanStatusBreakdown = statusBreakdown
        };
    }

    /// <summary>
    /// GET /reports/overdue-buckets — RPT-002
    /// Groups overdue loans by DPD bucket with counts and amounts
    /// </summary>
    public async Task<OverdueBucketReportDto> GetOverdueBucketReportAsync()
    {
        var overdueList = (await _loanService.GetOverdueListAsync()).ToList();

        var bucketOrder = new[] { DpdBucket.Current, DpdBucket.Dpd1To7, DpdBucket.Dpd8To30, DpdBucket.Dpd31Plus };

        var buckets = overdueList
            .GroupBy(x => x.Bucket)
            .Select(g => new OverdueBucketDto
            {
                Bucket = g.Key,
                Label = BucketLabel(g.Key),
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.OverdueDueAmount)
            })
            .OrderBy(b => Array.IndexOf(bucketOrder, b.Bucket))
            .ToList();

        return new OverdueBucketReportDto { Buckets = buckets };
    }

    private static string BucketLabel(string bucket) => bucket switch
    {
        DpdBucket.Current   => "Current",
        DpdBucket.Dpd1To7   => "1-7 Days Past Due",
        DpdBucket.Dpd8To30  => "8-30 Days Past Due",
        DpdBucket.Dpd31Plus => "31+ Days Past Due",
        _ => bucket
    };
}
