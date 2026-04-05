using LoanFlow.Application.DTOs.Dashboard;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync();
    Task UpdateOverdueStatusAsync(); // Called by background job
}

public class DashboardService : IDashboardService
{
    private readonly LoanFlowDbContext _db;

    public DashboardService(LoanFlowDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        var totalActiveLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Active);
        var totalOutstanding = await _db.Loans.Where(l => l.Status == LoanStatus.Active).SumAsync(l => l.OutstandingBalance);
        var disbursedThisMonth = await _db.LoanDisbursements.Where(d => d.DisbursedAt >= firstOfMonth).SumAsync(d => d.Amount);
        var newApps = await _db.LoanApplications.CountAsync(a => a.CreatedAt >= firstOfMonth);
        var pendingApprovals = await _db.LoanApplications.CountAsync(a => a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview);
        var overdueCount = await _db.DelinquencyStatuses.CountAsync(d => d.DaysOverdue > 0);
        var collectedThisMonth = await _db.Payments.Where(p => p.PaymentDate >= firstOfMonth).SumAsync(p => p.Amount);

        var buckets = await _db.DelinquencyStatuses
            .GroupBy(d => d.Bucket)
            .Select(g => new OverdueBucketDto
            {
                Bucket = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(d => d.OverdueAmount)
            })
            .ToListAsync();

        var statusBreakdown = await _db.Loans
            .GroupBy(l => l.Status)
            .Select(g => new LoanStatusSummaryDto
            {
                Status = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(l => l.OutstandingBalance)
            })
            .ToListAsync();

        // Label buckets
        var bucketLabels = new Dictionary<string, string>
        {
            [DpdBucket.Current] = "Hiện hành",
            [DpdBucket.Dpd1To7] = "Quá hạn 1-7 ngày",
            [DpdBucket.Dpd8To30] = "Quá hạn 8-30 ngày",
            [DpdBucket.Dpd31To90] = "Quá hạn 31-90 ngày",
            [DpdBucket.Dpd90Plus] = "Quá hạn >90 ngày"
        };
        foreach (var b in buckets)
            b.Label = bucketLabels.GetValueOrDefault(b.Bucket, b.Bucket);

        return new DashboardSummaryDto
        {
            TotalActiveLoans = totalActiveLoans,
            TotalOutstanding = totalOutstanding,
            TotalDisbursedThisMonth = disbursedThisMonth,
            NewApplicationsThisMonth = newApps,
            PendingApprovals = pendingApprovals,
            OverdueAccounts = overdueCount,
            CollectedThisMonth = collectedThisMonth,
            OverdueBuckets = buckets,
            LoanStatusBreakdown = statusBreakdown
        };
    }

    public async Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync()
    {
        return await _db.DelinquencyStatuses
            .Include(d => d.Loan).ThenInclude(l => l.Customer)
            .Include(d => d.AssignedCollector)
            .Include(d => d.CollectionNotes)
            .Where(d => d.DaysOverdue > 0)
            .OrderByDescending(d => d.DaysOverdue)
            .Select(d => new OverdueItemDto
            {
                LoanId = d.LoanId,
                LoanNumber = d.Loan.LoanNumber,
                CustomerId = d.Loan.CustomerId,
                CustomerName = d.Loan.Customer.FullName,
                CustomerPhone = d.Loan.Customer.PhoneNumber,
                OutstandingBalance = d.Loan.OutstandingBalance,
                OverdueAmount = d.OverdueAmount,
                DaysOverdue = d.DaysOverdue,
                Bucket = d.Bucket,
                AssignedCollector = d.AssignedCollector != null ? d.AssignedCollector.FullName : null,
                LastCollectionNote = d.CollectionNotes.OrderByDescending(n => n.CreatedAt).Select(n => n.Note).FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task UpdateOverdueStatusAsync()
    {
        var today = DateTime.Today;
        var activeLoans = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.DelinquencyStatus)
            .Where(l => l.Status == LoanStatus.Active)
            .ToListAsync();

        foreach (var loan in activeLoans)
        {
            var oldestOverdue = loan.RepaymentSchedules
                .Where(s => s.Status != ScheduleStatus.Paid && s.DueDate < today)
                .OrderBy(s => s.DueDate)
                .FirstOrDefault();

            int daysOverdue = oldestOverdue is not null ? (today - oldestOverdue.DueDate).Days : 0;
            var overdueAmount = loan.RepaymentSchedules
                .Where(s => s.Status != ScheduleStatus.Paid && s.DueDate < today)
                .Sum(s => s.TotalDue - s.PaidAmount);

            if (loan.DelinquencyStatus is null)
            {
                loan.DelinquencyStatus = new Domain.Entities.DelinquencyStatus
                {
                    LoanId = loan.Id,
                    DaysOverdue = daysOverdue,
                    Bucket = DpdBucket.Calculate(daysOverdue),
                    OverdueAmount = overdueAmount
                };
                _db.DelinquencyStatuses.Add(loan.DelinquencyStatus);
            }
            else
            {
                loan.DelinquencyStatus.DaysOverdue = daysOverdue;
                loan.DelinquencyStatus.Bucket = DpdBucket.Calculate(daysOverdue);
                loan.DelinquencyStatus.OverdueAmount = overdueAmount;
                loan.DelinquencyStatus.LastCheckedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }
}
