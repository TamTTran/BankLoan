using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ClosedXML.Excel;

namespace LoanFlow.Application.Services;

public class NhnnClassificationReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<NhnnClassificationRow> Rows { get; set; } = [];
    public decimal TotalOutstanding { get; set; }
    public decimal TotalProvision { get; set; }
    // Biểu 1A: Chi tiết
    public List<NhnnClassificationDetailRow> Details { get; set; } = [];
}

public class NhnnProvisionReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public decimal OpeningProvision { get; set; }
    public decimal NewProvision { get; set; }
    public decimal ReversedProvision { get; set; }
    public decimal ClosingProvision { get; set; }
    public List<NhnnProvisionGroupRow> GroupDetails { get; set; } = [];
}

public class CicPeriodicReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalRecords { get; set; }
    public List<CicLoanRecord> Records { get; set; } = [];
}
