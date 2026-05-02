using LoanFlow.Application.DTOs.Customer;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class CustomerLeadService : ICustomerLeadService
{
    private readonly LoanFlowDbContext _db;

    public CustomerLeadService(LoanFlowDbContext db) => _db = db;

    public async Task<List<CustomerLeadDto>> GetLeadBoardAsync(int? assignedToUserId = null)
    {
        // ── Step 1: Load customers ──────────────────────────────────────────
        var customerQuery = _db.Customers
            .Include(c => c.AssignedToUser)
            .Where(c => c.Status == "Active");

        if (assignedToUserId.HasValue)
            customerQuery = customerQuery.Where(c => c.AssignedToUserId == assignedToUserId.Value);

        var customers = await customerQuery.ToListAsync();
        if (customers.Count == 0) return [];

        var customerIds = customers.Select(c => c.Id).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── Step 2: Batch-load related data ─────────────────────────────────
        var loans = await _db.Loans
            .Where(l => customerIds.Contains(l.CustomerId))
            .Select(l => new
            {
                l.CustomerId,
                l.Status,
                l.MaturityDate,
                l.OutstandingAmount
            })
            .ToListAsync();

        var repaymentStats = await _db.RepaymentSchedules
            .Where(rs => customerIds.Contains(rs.Loan.CustomerId))
            .GroupBy(rs => rs.Loan.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalInstallments = g.Count(),
                PaidOnTime = g.Count(rs => rs.Status == InstallmentStatus.Paid)
            })
            .ToListAsync();

        var creditChecks = await _db.CreditChecks
            .Where(cc => customerIds.Contains(cc.CustomerId))
            .GroupBy(cc => cc.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                LatestGrade = g.OrderByDescending(cc => cc.CheckedAt).First().CreditGrade
            })
            .ToListAsync();

        var rejectedApps = await _db.LoanApplications
            .Where(a => customerIds.Contains(a.CustomerId) && a.Status == ApplicationStatus.Rejected)
            .GroupBy(a => a.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                LatestRejectDate = g.Max(a => a.UpdatedAt)
            })
            .ToListAsync();

        var lastInteractions = await _db.CustomerCareNotes
            .Where(n => customerIds.Contains(n.CustomerId))
            .GroupBy(n => n.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                LastDate = g.Max(n => n.InteractionDate)
            })
            .ToListAsync();

        // ── Step 3: Score each customer ─────────────────────────────────────
        var results = new List<CustomerLeadDto>();

        foreach (var c in customers)
        {
            int score = 0;
            var signals = new List<string>();

            var customerLoans = loans.Where(l => l.CustomerId == c.Id).ToList();
            var activeLoans = customerLoans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue).ToList();

            // Signal 1: Khoản vay sắp tất toán (≤30 ngày) → +30
            var nearMaturity = activeLoans.Any(l =>
                l.MaturityDate.HasValue &&
                l.MaturityDate.Value <= today.AddDays(30) &&
                l.MaturityDate.Value >= today);

            if (nearMaturity)
            {
                score += 30;
                signals.Add("Khoản vay sắp tất toán (≤30 ngày) — cơ hội cross-sell");
            }

            // Signal 2: Trả nợ đúng hạn ≥80% → +20
            var repayment = repaymentStats.FirstOrDefault(r => r.CustomerId == c.Id);
            if (repayment is { TotalInstallments: > 0 })
            {
                var onTimeRate = (double)repayment.PaidOnTime / repayment.TotalInstallments;
                if (onTimeRate >= 0.8)
                {
                    score += 20;
                    signals.Add($"Trả đúng hạn {onTimeRate:P0} — khách tín nhiệm cao");
                }
            }

            // Signal 3: Credit Grade A/B → +15
            var credit = creditChecks.FirstOrDefault(cc => cc.CustomerId == c.Id);
            if (credit?.LatestGrade is CreditGrade.A or CreditGrade.B)
            {
                score += 15;
                signals.Add($"Credit Grade {credit.LatestGrade} — profile đáng tin cậy");
            }

            // Signal 4: Khách "ngủ đông" — có hồ sơ >60 ngày, chưa có khoản vay → +15
            var isDormant = !customerLoans.Any() &&
                            c.CreatedAt < DateTime.UtcNow.AddDays(-60);
            if (isDormant)
            {
                score += 15;
                signals.Add("Khách ngủ đông >60 ngày — cần nurture");
            }

            // Signal 5: Từng bị reject >90 ngày trước → +10 (re-engagement)
            var rejected = rejectedApps.FirstOrDefault(r => r.CustomerId == c.Id);
            if (rejected != null && rejected.LatestRejectDate < DateTime.UtcNow.AddDays(-90))
            {
                score += 10;
                signals.Add("Từng bị từ chối >90 ngày — re-engagement");
            }

            // Signal 6: Đang Overdue → -20 (chuyển cho Collector)
            var hasOverdue = customerLoans.Any(l => l.Status == LoanStatus.Overdue);
            if (hasOverdue)
            {
                score -= 20;
                signals.Add("⚠️ Đang có khoản vay quá hạn — chuyển Collector");
            }

            // Clamp score
            score = Math.Clamp(score, 0, 100);

            var lastInteraction = lastInteractions.FirstOrDefault(li => li.CustomerId == c.Id);

            results.Add(new CustomerLeadDto
            {
                CustomerId = c.Id,
                FullName = c.FullName,
                Phone = c.Phone,
                LeadScore = score,
                LeadPriority = LeadPriority.FromScore(score),
                Signals = signals,
                AssignedToFullName = c.AssignedToUser?.FullName,
                LastInteractionDate = lastInteraction?.LastDate
            });
        }

        // ── Step 4: Sort by score descending ────────────────────────────────
        return results.OrderByDescending(r => r.LeadScore).ThenBy(r => r.FullName).ToList();
    }
}
