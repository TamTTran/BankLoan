using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using System.Text;

namespace LoanFlow.Application.Services;

public class DataExportDto
{
    public string DatasetName { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int RecordCount { get; set; }
    public string CsvContent { get; set; } = "";
}

public class PortfolioHealthDto
{
    public DateOnly AsOfDate { get; set; }
    public decimal NplRatio { get; set; }
    public decimal ProvisionCoverage { get; set; }
    public decimal AvgDpd { get; set; }
    public int TotalLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal CollateralCoverage { get; set; }     // Total collateral value / Total outstanding
    public List<NplTrendPoint> NplTrend { get; set; } = [];
    public List<BranchComparisonItem> BranchComparison { get; set; } = [];
}

public class NplTrendPoint
{
    public DateOnly Date { get; set; }
    public decimal NplRatio { get; set; }
    public decimal Outstanding { get; set; }
}

public class BranchComparisonItem
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int TotalLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int NplLoans { get; set; }
    public decimal NplRatio { get; set; }
}
