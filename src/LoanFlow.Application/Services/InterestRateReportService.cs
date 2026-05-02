using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class InterestRateReportService : IInterestRateReportService
{
    private readonly LoanFlowDbContext _db;

    public InterestRateReportService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<InterestRateReportDto> GenerateReportAsync(int year, int month)
    {
        var loans = await _db.Loans
            .Where(l => l.Status == Domain.Constants.LoanStatus.Active)
            .Select(l => new { l.ProductType, l.TenorMonths, l.MonthlyInterestRate, l.OutstandingAmount })
            .ToListAsync();

        if (!loans.Any()) return new InterestRateReportDto { Year = year, Month = month };

        // Weighted Average Interest Rate (Annualized)
        decimal totalOutstanding = loans.Sum(l => l.OutstandingAmount);
        decimal weightedSum = loans.Sum(l => l.OutstandingAmount * (l.MonthlyInterestRate * 12));
        decimal overallAvg = totalOutstanding > 0 ? weightedSum / totalOutstanding : 0;

        // By Product
        var byProduct = loans.GroupBy(l => l.ProductType).Select(g =>
        {
            var productOut = g.Sum(l => l.OutstandingAmount);
            var productSum = g.Sum(l => l.OutstandingAmount * (l.MonthlyInterestRate * 12));
            return new InterestRateByProductDto
            {
                ProductType = g.Key,
                TotalOutstanding = productOut,
                AverageInterestRate = productOut > 0 ? Math.Round(productSum / productOut, 2) : 0
            };
        }).ToList();

        // By Tenor
        var byTenor = loans.GroupBy(l => GetTenorBucket(l.TenorMonths)).Select(g =>
        {
            var tenorOut = g.Sum(l => l.OutstandingAmount);
            var tenorSum = g.Sum(l => l.OutstandingAmount * (l.MonthlyInterestRate * 12));
            return new InterestRateByTenorDto
            {
                TenorBucket = g.Key,
                TotalOutstanding = tenorOut,
                AverageInterestRate = tenorOut > 0 ? Math.Round(tenorSum / tenorOut, 2) : 0
            };
        }).ToList();

        return new InterestRateReportDto
        {
            Year = year, Month = month,
            OverallAverageInterestRate = Math.Round(overallAvg, 2),
            ByProduct = byProduct,
            ByTenor = byTenor
        };
    }

    private string GetTenorBucket(int months)
    {
        if (months <= 6) return "0-6 months";
        if (months <= 12) return "6-12 months";
        if (months <= 36) return "1-3 years";
        return ">3 years";
    }
}
