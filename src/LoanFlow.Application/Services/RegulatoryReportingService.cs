using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ClosedXML.Excel;

namespace LoanFlow.Application.Services;

public class NhnnClassificationDetailRow
{
    public string LoanAccountNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerIdNumber { get; set; } = "";
    public int Group { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal RequiredProvision { get; set; }
    public decimal ActualProvision { get; set; }
}

public class NhnnClassificationRow
{
    public int Group { get; set; }
    public string GroupName { get; set; } = "";
    public int LoanCount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal ProvisionRate { get; set; }          // % per NHNN
    public decimal RequiredProvision { get; set; }
    public decimal ActualProvision { get; set; }
    public decimal ProvisionGap { get; set; }           // Required - Actual
}

public class NhnnProvisionGroupRow
{
    public int Group { get; set; }
    public string GroupName { get; set; } = "";
    public decimal OpeningBalance { get; set; }
    public decimal Additions { get; set; }
    public decimal Reversals { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class CicLoanRecord
{
    public string LoanAccountNo { get; set; } = "";
    public string CustomerIdNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal OutstandingAmount { get; set; }
    public string LoanStatus { get; set; } = "";
    public int Classification { get; set; }
    public int Dpd { get; set; }
    public DateOnly? DisbursementDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public string? IndustryCode { get; set; }
    public string? RegionCode { get; set; }
    public string? PurposeCode { get; set; }
}

public class RegulatoryReportingService : IRegulatoryReportingService
{
    private readonly LoanFlowDbContext _db;

    private static readonly Dictionary<int, (string Name, decimal Rate)> NhnnGroups = new()
    {
        { 1, ("Nợ đủ tiêu chuẩn (Nhóm 1)", 0m) },
        { 2, ("Nợ cần chú ý (Nhóm 2)", 5m) },
        { 3, ("Nợ dưới tiêu chuẩn (Nhóm 3)", 20m) },
        { 4, ("Nợ nghi ngờ (Nhóm 4)", 50m) },
        { 5, ("Nợ có khả năng mất vốn (Nhóm 5)", 100m) }
    };

    public RegulatoryReportingService(LoanFlowDbContext db) => _db = db;

    public async Task<NhnnClassificationReportDto> GenerateNhnnClassificationReportAsync(int year, int month)
    {
        var loans = await _db.Loans
            .Include(l => l.Customer)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToListAsync();

        var rows = Enumerable.Range(1, 5).Select(g =>
        {
            var groupLoans = loans.Where(l => l.CurrentClassification == g).ToList();
            var outstanding = groupLoans.Sum(l => l.OutstandingAmount);
            var rate = NhnnGroups[g].Rate;
            var required = Math.Round(outstanding * rate / 100, 2);
            var actual = groupLoans.Sum(l => l.ProvisionAmount);

            return new NhnnClassificationRow
            {
                Group = g,
                GroupName = NhnnGroups[g].Name,
                LoanCount = groupLoans.Count,
                OutstandingBalance = outstanding,
                ProvisionRate = rate,
                RequiredProvision = required,
                ActualProvision = actual,
                ProvisionGap = required - actual
            };
        }).ToList();

        var details = loans.Select(l => new NhnnClassificationDetailRow
        {
            LoanAccountNo = l.LoanAccountNo,
            CustomerName = l.Customer.FullName,
            CustomerIdNumber = l.Customer.IdNumber,
            Group = l.CurrentClassification,
            OutstandingBalance = l.OutstandingAmount,
            ActualProvision = l.ProvisionAmount,
            RequiredProvision = Math.Round(l.OutstandingAmount * NhnnGroups[l.CurrentClassification].Rate / 100, 2)
        }).ToList();

        return new NhnnClassificationReportDto
        {
            Year = year, Month = month,
            Rows = rows,
            Details = details,
            TotalOutstanding = rows.Sum(r => r.OutstandingBalance),
            TotalProvision = rows.Sum(r => r.ActualProvision)
        };
    }

    public async Task<NhnnProvisionReportDto> GenerateNhnnProvisionReportAsync(int year, int month)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        // Get provision journal entries for the period
        var provisionJournals = await _db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.BusinessDate >= periodStart && j.BusinessDate <= periodEnd
                     && j.ReferenceNumber.StartsWith("PROV-"))
            .ToListAsync();

        // Calculate additions (Dr 5001 Provision Expense) and reversals (Cr 5001)
        var provExpenseAccount = await _db.GLAccounts.FirstOrDefaultAsync(a => a.AccountCode == "5001");
        decimal additions = 0, reversals = 0;

        if (provExpenseAccount != null)
        {
            foreach (var journal in provisionJournals)
            {
                foreach (var line in journal.Lines.Where(l => l.GLAccountId == provExpenseAccount.Id))
                {
                    additions += line.DebitAmount;
                    reversals += line.CreditAmount;
                }
            }
        }

        // Current provision breakdown by group
        var loans = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .Select(l => new { l.CurrentClassification, l.ProvisionAmount })
            .ToListAsync();

        var closingProvision = loans.Sum(l => l.ProvisionAmount);
        var openingProvision = closingProvision - additions + reversals;

        var groupDetails = Enumerable.Range(1, 5).Select(g =>
        {
            var groupProv = loans.Where(l => l.CurrentClassification == g).Sum(l => l.ProvisionAmount);
            return new NhnnProvisionGroupRow
            {
                Group = g,
                GroupName = NhnnGroups[g].Name,
                ClosingBalance = groupProv,
                // Simplified: exact per-group opening/addition/reversal requires
                // snapshot history. Using closing as proxy for now.
                OpeningBalance = 0,
                Additions = 0,
                Reversals = 0
            };
        }).ToList();

        return new NhnnProvisionReportDto
        {
            Year = year, Month = month,
            OpeningProvision = openingProvision,
            NewProvision = additions,
            ReversedProvision = reversals,
            ClosingProvision = closingProvision,
            GroupDetails = groupDetails
        };
    }

    public async Task<CicPeriodicReportDto> GenerateCicPeriodicReportAsync(int year, int month)
    {
        var loans = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.RepaymentSchedules)
            .Where(l => l.Status != LoanStatus.Closed)
            .ToListAsync();

        var records = loans.Select(l =>
        {
            var today = DateTime.UtcNow;
            var unpaid = l.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid && s.DueDate.ToDateTime(TimeOnly.MinValue) < today)
                .ToList();
            int maxDpd = 0;
            if (unpaid.Any())
            {
                var oldest = unpaid.Min(s => s.DueDate.ToDateTime(TimeOnly.MinValue));
                maxDpd = (int)(today - oldest).TotalDays;
            }

            return new CicLoanRecord
            {
                LoanAccountNo = l.LoanAccountNo,
                CustomerIdNumber = l.Customer.IdNumber,
                CustomerName = l.Customer.FullName,
                OutstandingAmount = l.OutstandingAmount,
                LoanStatus = l.Status,
                Classification = l.CurrentClassification,
                Dpd = maxDpd,
                DisbursementDate = l.DisbursementDate,
                MaturityDate = l.MaturityDate,
                IndustryCode = l.IndustryCode ?? l.Customer.IndustryCode,
                RegionCode = l.Customer.RegionCode,
                PurposeCode = l.PurposeCode
            };
        }).ToList();

        return new CicPeriodicReportDto
        {
            Year = year, Month = month,
            TotalRecords = records.Count,
            Records = records
        };
    }

    public string ToCsv<T>(IEnumerable<T> items)
    {
        var sb = new StringBuilder();
        var properties = typeof(T).GetProperties();

        // Header
        sb.AppendLine(string.Join(",", properties.Select(p => p.Name)));

        // Rows
        foreach (var item in items)
        {
            var values = properties.Select(p =>
            {
                var val = p.GetValue(item)?.ToString() ?? "";
                // Escape commas and quotes
                if (val.Contains(',') || val.Contains('"'))
                    val = $"\"{val.Replace("\"", "\"\"")}\"";
                return val;
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    public byte[] ToExcel<T>(IEnumerable<T> items, string sheetName = "Data")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        
        var properties = typeof(T).GetProperties();

        // Header
        for (int i = 0; i < properties.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = properties[i].Name;
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        // Rows
        var itemList = items.ToList();
        for (int row = 0; row < itemList.Count; row++)
        {
            for (int col = 0; col < properties.Length; col++)
            {
                var val = properties[col].GetValue(itemList[row]);
                if (val != null)
                {
                    worksheet.Cell(row + 2, col + 1).Value = XLCellValue.FromObject(val);
                }
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public string ToXml<T>(IEnumerable<T> items, string rootName = "Data")
    {
        var wrapper = new XmlRootWrapper<T> { Items = items.ToList() };
        var xmlSerializer = new XmlSerializer(typeof(XmlRootWrapper<T>), new XmlRootAttribute(rootName));
        
        var emptyNamespaces = new XmlSerializerNamespaces([XmlQualifiedName.Empty]);
        var settings = new System.Xml.XmlWriterSettings 
        { 
            Indent = true,
            OmitXmlDeclaration = true
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = System.Xml.XmlWriter.Create(stringWriter, settings);
        
        xmlSerializer.Serialize(xmlWriter, wrapper, emptyNamespaces);
        return stringWriter.ToString();
    }
}

public class XmlRootWrapper<T>
{
    [XmlElement("Item")]
    public List<T> Items { get; set; } = new();
}
