using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Application.Services;

public class TransactionMonitoringService : ITransactionMonitoringService
{
    private readonly LoanFlowDbContext _db;

    public TransactionMonitoringService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task MonitorDailyTransactionsAsync(DateOnly date)
    {
        // 1. Large Cash Transactions (CTR rule) - e.g. ≥ 300M VND
        decimal largeThreshold = 300_000_000m;

        var largeDisbursements = await _db.LoanDisbursements
            .Include(d => d.Loan)
            .Where(d => d.DisbursementDate == date && d.DisbursedAmount >= largeThreshold)
            .ToListAsync();

        foreach (var d in largeDisbursements)
        {
            var str = new SuspiciousTransactionReport
            {
                CustomerId = d.Loan.CustomerId,
                StrNumber = $"STR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                SuspicionType = "LargeCash",
                Description = $"Large disbursement detected: {d.DisbursedAmount:N0} VND for Loan #{d.LoanId}.",
                RelatedTransactionIdsJson = JsonSerializer.Serialize(new[] { $"DISB-{d.Id}" }),
                CreatedByUserId = 1 // System user
            };
            _db.SuspiciousTransactionReports.Add(str);
        }

        var largePayments = await _db.Payments
            .Where(p => p.PaymentDate == date && p.Amount >= largeThreshold)
            .ToListAsync();

        foreach (var p in largePayments)
        {
            // Payment doesn't have CustomerId directly, need to map via Loan or keep track
            // Let's assume Payment relates to Loan
            // We need to fetch the customer id. This is simplified.
            int customerId = 0; // Would query via Loan
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == p.LoanId);
            if (loan != null) customerId = loan.CustomerId;

            var str = new SuspiciousTransactionReport
            {
                CustomerId = customerId,
                StrNumber = $"STR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                SuspicionType = "LargeCash",
                Description = $"Large repayment detected: {p.Amount:N0} VND for Loan #{p.LoanId}.",
                RelatedTransactionIdsJson = JsonSerializer.Serialize(new[] { $"PMT-{p.Id}" }),
                CreatedByUserId = 1
            };
            _db.SuspiciousTransactionReports.Add(str);
        }

        // 2. Structuring Detection (Multiple transactions just below threshold)
        // Check customers who made multiple payments today that sum up to > 300M
        var structuredPayments = await _db.Payments
            .Where(p => p.PaymentDate == date && p.Amount < largeThreshold && p.Amount >= 50_000_000m)
            .GroupBy(p => p.LoanId)
            .Select(g => new { LoanId = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count(), Ids = g.Select(x => x.Id).ToList() })
            .Where(g => g.Total >= largeThreshold && g.Count >= 2)
            .ToListAsync();

        foreach (var g in structuredPayments)
        {
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == g.LoanId);
            if (loan == null) continue;

            var str = new SuspiciousTransactionReport
            {
                CustomerId = loan.CustomerId,
                StrNumber = $"STR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                SuspicionType = "Structuring",
                Description = $"Structuring detected: {g.Count} payments totaling {g.Total:N0} VND.",
                RelatedTransactionIdsJson = JsonSerializer.Serialize(g.Ids.Select(id => $"PMT-{id}")),
                CreatedByUserId = 1
            };
            _db.SuspiciousTransactionReports.Add(str);
        }

        await _db.SaveChangesAsync();
    }
}
