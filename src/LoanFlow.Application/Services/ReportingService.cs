using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class ReportingService : IReportingService
{
    private readonly LoanFlowDbContext _db;

    public ReportingService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<PortfolioReportDto> GetPortfolioReportAsync(DateOnly? asOfDate)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(date.Year, date.Month, 1);

        var loans = await _db.Loans.ToListAsync();

        var statusBreakdown = loans
            .GroupBy(l => l.Status)
            .Select(g => new StatusBreakdownItem
            {
                Status = g.Key,
                Count = g.Count(),
                TotalOutstanding = g.Sum(l => l.OutstandingAmount)
            }).ToList();

        var productBreakdown = loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .GroupBy(l => l.ProductType)
            .Select(g => new ProductBreakdownItem
            {
                ProductType = g.Key,
                Count = g.Count(),
                TotalOutstanding = g.Sum(l => l.OutstandingAmount)
            }).ToList();

        var activeLoans = loans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue).ToList();

        var disbThisMonth = await _db.LoanDisbursements
            .Where(d => d.DisbursementDate >= monthStart && d.DisbursementDate <= date)
            .ToListAsync();

        return new PortfolioReportDto
        {
            AsOfDate = date,
            TotalLoans = loans.Count,
            TotalOutstanding = activeLoans.Sum(l => l.OutstandingAmount),
            StatusBreakdown = statusBreakdown,
            ProductBreakdown = productBreakdown,
            AvgTenorMonths = activeLoans.Count > 0 ? Math.Round((decimal)activeLoans.Average(l => l.TenorMonths), 1) : 0,
            AvgInterestRate = activeLoans.Count > 0 ? Math.Round(activeLoans.Average(l => l.MonthlyInterestRate) * 100, 2) : 0,
            DisbursementsThisMonth = disbThisMonth.Count,
            DisbursementVolumeThisMonth = disbThisMonth.Sum(d => d.DisbursedAmount)
        };
    }

    public async Task<OverdueMovementReportDto> GetOverdueMovementReportAsync(DateOnly from, DateOnly to)
    {
        var snapshots = await _db.OverdueDailySnapshots
            .Where(s => s.SnapshotDate >= from && s.SnapshotDate <= to)
            .ToListAsync();

        var days = snapshots
            .GroupBy(s => s.SnapshotDate)
            .OrderBy(g => g.Key)
            .Select(g => new OverdueMovementDayDto
            {
                Date = g.Key,
                TotalOverdueLoans = g.Count(),
                TotalOverdueAmount = g.Sum(s => s.OverdueAmount),
                AvgDpd = g.Count() > 0 ? Math.Round((decimal)g.Average(s => s.Dpd), 1) : 0,
                BucketCounts = g.GroupBy(s => s.Bucket).ToDictionary(bg => bg.Key, bg => bg.Count())
            }).ToList();

        // New overdue: loans in today's snapshot but not in yesterday's (approximate)
        var firstDayLoanIds = snapshots.Where(s => s.SnapshotDate == from).Select(s => s.LoanId).ToHashSet();
        var lastDayLoanIds = snapshots.Where(s => s.SnapshotDate == to).Select(s => s.LoanId).ToHashSet();
        var newOverdue = lastDayLoanIds.Except(firstDayLoanIds).Count();
        var cured = firstDayLoanIds.Except(lastDayLoanIds).Count();

        return new OverdueMovementReportDto
        {
            From = from,
            To = to,
            Days = days,
            NewOverdueCount = newOverdue,
            CuredCount = cured
        };
    }

    public async Task<CollectionProductivityDto> GetCollectionProductivityAsync(DateOnly from, DateOnly to)
    {
        var actions = await _db.CollectionActions
            .Include(a => a.Collector)
            .Where(a => a.ActionDate >= from && a.ActionDate <= to)
            .ToListAsync();

        var assignments = await _db.CollectorAssignments
            .Include(a => a.Collector)
            .Where(a => a.Status == CollectorAssignmentStatus.Active)
            .ToListAsync();

        var collectorIds = actions.Select(a => a.CollectorUserId)
            .Union(assignments.Select(a => a.CollectorUserId))
            .Distinct();

        var collectors = new List<CollectorProductivityItem>();

        foreach (var cId in collectorIds)
        {
            var cActions = actions.Where(a => a.CollectorUserId == cId).ToList();
            var cAssignments = assignments.Count(a => a.CollectorUserId == cId);
            var contacted = cActions.Count(a => a.Outcome != CollectionOutcome.NoAnswer);
            var promises = cActions.Count(a => a.Outcome == CollectionOutcome.PromiseToPay);
            var kept = cActions.Count(a => a.PromiseStatus == Domain.Constants.PromiseStatus.Kept);

            collectors.Add(new CollectorProductivityItem
            {
                CollectorUserId = cId,
                CollectorName = cActions.FirstOrDefault()?.Collector?.FullName
                                ?? assignments.FirstOrDefault(a => a.CollectorUserId == cId)?.Collector?.FullName ?? "",
                TotalAssigned = cAssignments,
                TotalActions = cActions.Count,
                Contacted = contacted,
                ContactRate = cActions.Count > 0 ? Math.Round((decimal)contacted / cActions.Count * 100, 1) : 0,
                PromisesMade = promises,
                PromisesKept = kept,
                PromiseKeepRate = promises > 0 ? Math.Round((decimal)kept / promises * 100, 1) : 0
            });
        }

        return new CollectionProductivityDto { From = from, To = to, Collectors = collectors };
    }

    public async Task<ApprovalQueueReportDto> GetApprovalQueueReportAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var pending = await _db.LoanApplications
            .Where(a => a.Status == ApplicationStatus.Submitted)
            .ToListAsync();

        var processed = await _db.ApplicationDecisions
            .Include(d => d.DecidedBy)
            .Where(d => d.DecidedAt >= monthStart)
            .ToListAsync();

        var approved = processed.Count(d => d.Decision == "Approved");
        var rejected = processed.Count(d => d.Decision == "Rejected");
        var total = approved + rejected;

        var avgDaysPending = pending.Count > 0
            ? Math.Round(pending.Average(a => (now - a.CreatedAt).TotalDays), 1)
            : 0;

        var byApprover = processed
            .GroupBy(d => d.DecidedBy?.FullName ?? "Unknown")
            .Select(g => new ApproverSummaryItem
            {
                ApproverName = g.Key,
                Pending = 0,
                Approved = g.Count(d => d.Decision == "Approved"),
                Rejected = g.Count(d => d.Decision == "Rejected")
            }).ToList();

        return new ApprovalQueueReportDto
        {
            TotalPending = pending.Count,
            AvgDaysPending = (decimal)avgDaysPending,
            ApprovedThisMonth = approved,
            RejectedThisMonth = rejected,
            ApprovalRate = total > 0 ? Math.Round((decimal)approved / total * 100, 1) : 0,
            ByApprover = byApprover
        };
    }
}
