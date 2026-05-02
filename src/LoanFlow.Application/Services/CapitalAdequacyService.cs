using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class RiskWeightedAssetDetail
{
    public string AssetClass { get; set; } = "";
    public decimal OutstandingAmount { get; set; }
    public decimal RiskWeight { get; set; }
    public decimal RiskWeightedAmount => OutstandingAmount * RiskWeight / 100;
}

public class CapitalAdequacyService : ICapitalAdequacyService
{
    private readonly LoanFlowDbContext _db;

    public CapitalAdequacyService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<CarReportDto> CalculateCarAsync(int year, int month)
    {
        // 1. Calculate Own Capital (Vốn tự có)
        // Simplified: Tier 1 = Paid-in Capital + Retained Earnings
        // Tier 2 = Provision (up to 1.25% of RWA) + Subordinated Debt
        // Since we don't have a full GL yet, we'll mock Tier 1 and Tier 2 based on some GL accounts if they exist, or use fixed values.
        
        var equityAccounts = await _db.GLAccounts
            .Where(a => a.AccountCode.StartsWith("4") && a.AccountType == GLAccountType.Equity)
            .ToListAsync();

        decimal tier1Capital = 50000000000m; // Mock 50 tỷ VND
        decimal tier2Capital = 10000000000m; // Mock 10 tỷ VND

        // 2. Calculate Risk-Weighted Assets (TSRR)
        var loans = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .Select(l => new { l.OutstandingAmount, l.ProductType })
            .ToListAsync();

        var details = new List<RiskWeightedAssetDetail>();

        // Phân loại tài sản theo Risk Weight (TT41/2016)
        // Personal Unsecured: 75%
        // SME / Corporate: 100%
        // Real Estate: 50%
        // Others: 100%

        var grouped = loans.GroupBy(l => l.ProductType);
        foreach (var group in grouped)
        {
            decimal rw = group.Key switch
            {
                "PersonalUnsecured" => 75m,
                "SME" => 100m,
                "RealEstate" => 50m,
                _ => 100m
            };

            details.Add(new RiskWeightedAssetDetail
            {
                AssetClass = group.Key,
                OutstandingAmount = group.Sum(l => l.OutstandingAmount),
                RiskWeight = rw
            });
        }

        // Add Cash (Risk weight 0%)
        var cashAccount = await _db.GLAccounts.FirstOrDefaultAsync(a => a.AccountCode == "1001");
        // Mock cash amount
        details.Add(new RiskWeightedAssetDetail
        {
            AssetClass = "Cash",
            OutstandingAmount = 100000000000m, // 100 tỷ
            RiskWeight = 0m
        });

        var rwa = details.Sum(d => d.RiskWeightedAmount);

        return new CarReportDto
        {
            Year = year,
            Month = month,
            Tier1Capital = tier1Capital,
            Tier2Capital = tier2Capital,
            TotalRiskWeightedAssets = rwa,
            Details = details
        };
    }
}
