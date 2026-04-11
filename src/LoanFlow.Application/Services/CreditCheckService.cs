using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class CreditCheckDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? ApplicationId { get; set; }
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int ClosedLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int MaxDpdLast12Months { get; set; }
    public int CreditScore { get; set; }
    public string CreditGrade { get; set; } = default!;
    public string Recommendation { get; set; } = default!;
    public DateTime CheckedAt { get; set; }
}

public interface ICreditCheckService
{
    Task<Result<CreditCheckDto>> PerformCreditCheckAsync(int customerId, int? applicationId, int checkedByUserId);
    Task<IEnumerable<CreditCheckDto>> GetHistoryAsync(int customerId);
}

public class CreditCheckService : ICreditCheckService
{
    private readonly LoanFlowDbContext _db;

    public CreditCheckService(LoanFlowDbContext db) => _db = db;

    public async Task<Result<CreditCheckDto>> PerformCreditCheckAsync(int customerId, int? applicationId, int checkedByUserId)
    {
        var customer = await _db.Customers
            .Include(c => c.Loans)
                .ThenInclude(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer is null) return Result.Failure<CreditCheckDto>("Customer not found.");

        // Gather metrics
        var loans = customer.Loans;
        int totalLoans = loans.Count;
        int activeLoans = loans.Count(l => l.Status == LoanFlow.Domain.Constants.LoanStatus.Active);
        int overdueLoans = loans.Count(l => l.Status == LoanFlow.Domain.Constants.LoanStatus.Overdue);
        int closedLoans = loans.Count(l => l.Status == LoanFlow.Domain.Constants.LoanStatus.Closed);
        decimal totalOutstanding = loans.Where(l => l.Status != LoanFlow.Domain.Constants.LoanStatus.Closed)
                                        .Sum(l => l.OutstandingAmount);
        
        // Calculate Max DPD in last 12 months
        var oneYearAgo = DateTime.UtcNow.AddMonths(-12);
        int maxDpd = 0;
        
        // Simplified DPD estimation for past 12 months
        // We use query time DPD calculating: if schedule was due in last 12 months, find maximum delay.
        // Since we don't have historical DPD snapshots, we check un-paid schedules and their due dates.
        foreach (var loan in loans)
        {
            var targetSchedules = loan.RepaymentSchedules.Where(s => s.DueDate.ToDateTime(TimeOnly.MinValue) >= oneYearAgo);
            foreach (var s in targetSchedules)
            {
                if (s.Status != LoanFlow.Domain.Constants.InstallmentStatus.Paid)
                {
                    int dpd = (DateTime.UtcNow - s.DueDate.ToDateTime(TimeOnly.MinValue)).Days;
                    if (dpd > maxDpd) maxDpd = dpd;
                }
            }
        }

        // Scoring algorithm
        int score = 100;
        score -= overdueLoans * 20;
        score -= activeLoans * 5;
        
        if (maxDpd > 30) score -= 30;
        else if (maxDpd > 7) score -= 15;

        // If outstanding is > 80% of total borrowed ever
        decimal totalBorrowed = loans.Sum(l => l.ApprovedAmount);
        if (totalBorrowed > 0 && totalOutstanding > totalBorrowed * 0.8m)
            score -= 10;

        score += closedLoans * 10;

        // Clamp
        if (score > 100) score = 100;
        if (score < 0) score = 0;

        // Grade mapping
        string grade;
        string recommendation;

        if (score >= 80)
        {
            grade = "A";
            recommendation = "Approve";
        }
        else if (score >= 60)
        {
            grade = "B";
            recommendation = "Approve";
        }
        else if (score >= 40)
        {
            grade = "C";
            recommendation = "Review";
        }
        else if (score >= 20)
        {
            grade = "D";
            recommendation = "Review";
        }
        else
        {
            grade = "E";
            recommendation = "Reject";
        }

        var entity = new CreditCheck
        {
            CustomerId = customerId,
            ApplicationId = applicationId,
            TotalLoans = totalLoans,
            ActiveLoans = activeLoans,
            OverdueLoans = overdueLoans,
            ClosedLoans = closedLoans,
            TotalOutstanding = totalOutstanding,
            MaxDpdLast12Months = maxDpd,
            CreditScore = score,
            CreditGrade = grade,
            Recommendation = recommendation,
            CheckedByUserId = checkedByUserId
        };

        _db.CreditChecks.Add(entity);

        // Map back to customer to update their main credit status
        customer.CreditScore = score;
        customer.CreditStatus = grade; // Use Grade as Credit Status for simplicity
        customer.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = checkedByUserId,
            ActionCode = "CREDIT_CHECK_PERFORMED",
            EntityType = "CreditCheck",
            EntityId = 0, // Entity not saved yet
            NewValuesJson = AuditHelper.ToJson(new { score, grade })
        });

        await _db.SaveChangesAsync();

        return Result.Success(new CreditCheckDto
        {
            Id = entity.Id, CustomerId = entity.CustomerId, ApplicationId = entity.ApplicationId,
            TotalLoans = entity.TotalLoans, ActiveLoans = entity.ActiveLoans, OverdueLoans = entity.OverdueLoans,
            ClosedLoans = entity.ClosedLoans, TotalOutstanding = entity.TotalOutstanding,
            MaxDpdLast12Months = entity.MaxDpdLast12Months, CreditScore = entity.CreditScore,
            CreditGrade = entity.CreditGrade, Recommendation = entity.Recommendation, CheckedAt = entity.CheckedAt
        });
    }

    public async Task<IEnumerable<CreditCheckDto>> GetHistoryAsync(int customerId)
    {
        return await _db.CreditChecks
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CheckedAt)
            .Select(c => new CreditCheckDto
            {
                Id = c.Id, CustomerId = c.CustomerId, ApplicationId = c.ApplicationId,
                TotalLoans = c.TotalLoans, ActiveLoans = c.ActiveLoans, OverdueLoans = c.OverdueLoans,
                ClosedLoans = c.ClosedLoans, TotalOutstanding = c.TotalOutstanding,
                MaxDpdLast12Months = c.MaxDpdLast12Months, CreditScore = c.CreditScore,
                CreditGrade = c.CreditGrade, Recommendation = c.Recommendation, CheckedAt = c.CheckedAt
            })
            .ToListAsync();
    }
}
