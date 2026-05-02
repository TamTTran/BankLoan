using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class CarReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public decimal Tier1Capital { get; set; }
    public decimal Tier2Capital { get; set; }
    public decimal TotalCapital => Tier1Capital + Tier2Capital;
    public decimal TotalRiskWeightedAssets { get; set; }
    public decimal CarRatio => TotalRiskWeightedAssets > 0 ? (TotalCapital / TotalRiskWeightedAssets) * 100 : 0;
    public bool IsCompliant => CarRatio >= 8.0m; // Min 8% theo TT41/2016
    public List<RiskWeightedAssetDetail> Details { get; set; } = [];
}
