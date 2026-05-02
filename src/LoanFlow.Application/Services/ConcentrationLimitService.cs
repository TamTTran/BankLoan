using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class ConcentrationLimitService : IConcentrationLimitService
{
    private readonly LoanFlowDbContext _db;

    public ConcentrationLimitService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<ConcentrationReportDto> GetConcentrationReportAsync()
    {
        // 1. Bank Equity (Vốn tự có) - Mocked for now, should come from Accounting Module
        var equityConfig = await _db.SystemConfigs.FirstOrDefaultAsync(s => s.Key == "BankEquity");
        decimal bankEquity = equityConfig != null && decimal.TryParse(equityConfig.Value, out var e) ? e : 3000_000_000_000m; // 3000 billion VND default

        var activeLoans = await _db.Loans
            .Include(l => l.Customer)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToListAsync();

        var totalPortfolio = activeLoans.Sum(l => l.OutstandingAmount);

        // 2. Industry Concentration
        var industryGroups = activeLoans
            .GroupBy(l => l.IndustryCode ?? "UNKNOWN")
            .Select(g => new IndustryConcentrationDto
            {
                IndustryCode = g.Key,
                TotalExposure = g.Sum(l => l.OutstandingAmount),
                PortfolioShare = totalPortfolio > 0 ? Math.Round(g.Sum(l => l.OutstandingAmount) / totalPortfolio * 100, 2) : 0,
                LimitPercentage = 15.0m
            })
            .OrderByDescending(i => i.TotalExposure)
            .ToList();

        // 3. Single Borrower Concentration
        var topBorrowers = activeLoans
            .GroupBy(l => new { l.CustomerId, l.Customer.FullName })
            .Select(g => new SingleBorrowerConcentrationDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.FullName,
                TotalExposure = g.Sum(l => l.OutstandingAmount),
                EquityShare = bankEquity > 0 ? Math.Round(g.Sum(l => l.OutstandingAmount) / bankEquity * 100, 2) : 0,
                LimitPercentage = 25.0m
            })
            .OrderByDescending(b => b.TotalExposure)
            .Take(50) // Top 50 borrowers
            .ToList();

        return new ConcentrationReportDto
        {
            AsOfDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalPortfolioExposure = totalPortfolio,
            BankEquity = bankEquity,
            IndustryConcentrations = industryGroups,
            TopBorrowers = topBorrowers
        };
    }
}
