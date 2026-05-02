using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class NplSummaryDto
{
    public DateOnly AsOfDate { get; set; }
    public int TotalActiveLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal NplOutstanding { get; set; }     // Groups 3-5
    public decimal NplRatio { get; set; }            // NplOutstanding / TotalOutstanding * 100
    public decimal TotalProvision { get; set; }
    public decimal ProvisionCoverageRatio { get; set; } // TotalProvision / NplOutstanding * 100
    public List<ClassificationGroupDto> Groups { get; set; } = [];
}

public class ClassificationGroupDto
{
    public int Group { get; set; }
    public string GroupName { get; set; } = "";
    public int LoanCount { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Provision { get; set; }
    public decimal ProvisionRate { get; set; }      // %
    public decimal ShareOfPortfolio { get; set; }   // %
}

public class MigrationMatrixDto
{
    public DateOnly FromMonth { get; set; }
    public DateOnly ToMonth { get; set; }
    public List<MigrationMatrixRow> Rows { get; set; } = [];
    public int TotalLoansAnalyzed { get; set; }
}

public class NplByDimensionDto
{
    public DateOnly AsOfDate { get; set; }
    public List<NplDimensionItem> Items { get; set; } = [];
}

public class NplDimensionItem
{
    public string DimensionKey { get; set; } = "";  // Product type or Officer name
    public int TotalLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int NplLoans { get; set; }               // Groups 3-5
    public decimal NplOutstanding { get; set; }
    public decimal NplRatio { get; set; }
    public decimal Provision { get; set; }
}
