namespace LoanFlow.Application.Services;

public class LiquidityReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    // LCR
    public decimal HighQualityLiquidAssets { get; set; }
    public decimal TotalNetCashOutflows30Days { get; set; }
    public decimal LcrRatio => TotalNetCashOutflows30Days > 0 ? (HighQualityLiquidAssets / TotalNetCashOutflows30Days) * 100 : 0;
    public bool IsLcrCompliant => LcrRatio >= 100.0m;

    // NSFR
    public decimal AvailableStableFunding { get; set; }
    public decimal RequiredStableFunding { get; set; }
    public decimal NsfrRatio => RequiredStableFunding > 0 ? (AvailableStableFunding / RequiredStableFunding) * 100 : 0;
    public bool IsNsfrCompliant => NsfrRatio >= 100.0m;
}
