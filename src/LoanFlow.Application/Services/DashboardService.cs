using LoanFlow.Application.DTOs.Dashboard;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<OverdueBucketReportDto> GetOverdueBucketReportAsync();
    Task<MonthlyTrendDto> GetMonthlyTrendAsync(int months = 6);
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
        var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var today = DateOnly.FromDateTime(now);
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var localTodayStart = today.ToDateTime(TimeOnly.MinValue);
        var utcTodayStart = TimeZoneInfo.ConvertTimeToUtc(localTodayStart, tz);
        var utcTomorrowStart = TimeZoneInfo.ConvertTimeToUtc(localTodayStart.AddDays(1), tz);
        var utcMonthStart = TimeZoneInfo.ConvertTimeToUtc(monthStart.ToDateTime(TimeOnly.MinValue), tz);
        var utcNextMonthStart = TimeZoneInfo.ConvertTimeToUtc(nextMonthStart.ToDateTime(TimeOnly.MinValue), tz);

        var dueTodayAmount = await _db.RepaymentSchedules
            .Where(s => s.DueDate == today && s.Status != InstallmentStatus.Paid)
            .SumAsync(s => s.RemainingDueAmount);

        var totalApplicationsToday = await _db.LoanApplications
            .CountAsync(a => a.CreatedAt >= utcTodayStart && a.CreatedAt < utcTomorrowStart);

        var pendingApprovals = await _db.LoanApplications
            .CountAsync(a => a.Status == ApplicationStatus.Submitted);

        var approvedApplicationsThisMonth = await _db.LoanApplications
            .CountAsync(a => a.Status == ApplicationStatus.Approved &&
                             a.UpdatedAt >= utcMonthStart &&
                             a.UpdatedAt < utcNextMonthStart);

        var rejectedApplicationsThisMonth = await _db.LoanApplications
            .CountAsync(a => a.Status == ApplicationStatus.Rejected &&
                             a.UpdatedAt >= utcMonthStart &&
                             a.UpdatedAt < utcNextMonthStart);

        var disbursedLoansThisMonth = await _db.Loans
            .CountAsync(l => l.DisbursementDate.HasValue &&
                             l.DisbursementDate.Value >= monthStart &&
                             l.DisbursementDate.Value < nextMonthStart);

        var totalDisbursedThisMonth = await _db.Loans
            .Where(l => l.DisbursementDate.HasValue &&
                        l.DisbursementDate.Value >= monthStart &&
                        l.DisbursementDate.Value < nextMonthStart)
            .SumAsync(l => l.ApprovedAmount);

        var collectedThisMonth = await _db.Payments
            .Where(p => p.PaymentDate >= monthStart && p.PaymentDate < nextMonthStart)
            .SumAsync(p => p.Amount);

        var statusBreakdown = await _db.Loans
            .GroupBy(l => l.Status)
            .Select(g => new LoanStatusSummaryDto
            {
                Status = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(l => l.OutstandingAmount)
            })
            .ToListAsync();

        var applicationStatusBreakdown = await _db.LoanApplications
            .GroupBy(a => a.Status)
            .Select(g => new ApplicationStatusSummaryDto
            {
                Status = g.Key,
                Count = g.Count(),
                TotalRequestedAmount = g.Sum(a => a.RequestedAmount)
            })
            .ToListAsync();

        return new DashboardSummaryDto
        {
            TotalActiveLoans = totalActiveLoans,
            TotalOverdueLoans = totalOverdueLoans,
            TotalApplicationsToday = totalApplicationsToday,
            ApprovedApplicationsThisMonth = approvedApplicationsThisMonth,
            RejectedApplicationsThisMonth = rejectedApplicationsThisMonth,
            DisbursedLoansThisMonth = disbursedLoansThisMonth,
            DueTodayAmount = dueTodayAmount,
            TotalOutstandingAmount = totalOutstanding,
            TotalDisbursedThisMonth = totalDisbursedThisMonth,
            CollectedThisMonth = collectedThisMonth,
            PendingApprovals = pendingApprovals,
            LoanStatusBreakdown = statusBreakdown,
            ApplicationStatusBreakdown = applicationStatusBreakdown
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

    /// <summary>
    /// GET /dashboard/monthly-trend — RPT-003
    /// Returns last N months of disbursement, collection and new-loan volume.
    /// </summary>
    public async Task<MonthlyTrendDto> GetMonthlyTrendAsync(int months = 6)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var startDate = new DateOnly(now.Year, now.Month, 1).AddMonths(-(months - 1));

        // Disbursed per month: sum of ApprovedAmount for loans disbursed in window
        var disbursed = await _db.Loans
            .Where(l => l.DisbursementDate.HasValue &&
                        l.DisbursementDate.Value >= startDate)
            .GroupBy(l => new { l.DisbursementDate!.Value.Year, l.DisbursementDate.Value.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Disbursed = g.Sum(l => l.ApprovedAmount),
                NewLoans = g.Count()
            })
            .ToListAsync();

        // Collected per month: sum of payments received in window
        var collected = await _db.Payments
            .Where(p => p.PaymentDate >= startDate)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Collected = g.Sum(p => p.Amount)
            })
            .ToListAsync();

        var points = new List<MonthlyTrendPointDto>();
        for (int i = 0; i < months; i++)
        {
            var d = startDate.AddMonths(i);
            var dis = disbursed.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            var col = collected.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            points.Add(new MonthlyTrendPointDto
            {
                Month = $"{d.Year}-{d.Month:D2}",
                Label = d.ToString("MMM yy"),
                Disbursed = dis?.Disbursed ?? 0,
                Collected = col?.Collected ?? 0,
                NewLoans  = dis?.NewLoans ?? 0,
            });
        }
        return new MonthlyTrendDto { Points = points };
    }
}
