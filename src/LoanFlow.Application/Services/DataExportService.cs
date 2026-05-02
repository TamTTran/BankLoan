using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace LoanFlow.Application.Services;

public class DataExportService : IDataExportService
{
    private readonly LoanFlowDbContext _db;

    public DataExportService(LoanFlowDbContext db) => _db = db;

    public async Task<DataExportDto> ExportLoansAsync()
    {
        var loans = await _db.Loans
            .Include(l => l.Customer)
            .Select(l => new
            {
                l.LoanAccountNo, CustomerName = l.Customer.FullName, l.Customer.IdNumber,
                l.Status, l.OutstandingAmount, l.ApprovedAmount, l.TenorMonths,
                l.MonthlyInterestRate, l.ProductType, l.CurrentClassification, l.ProvisionAmount,
                l.DisbursementDate, l.MaturityDate, l.BranchId
            }).ToListAsync();

        return new DataExportDto
        {
            DatasetName = "loans",
            RecordCount = loans.Count,
            CsvContent = ToCsv(loans)
        };
    }

    public async Task<DataExportDto> ExportCustomersAsync()
    {
        var customers = await _db.Customers
            .Select(c => new
            {
                c.Id, c.FullName, c.Phone, c.IdNumber, c.Address,
                c.DateOfBirth, c.Status, c.CreditScore, c.CreditStatus, c.BranchId
            }).ToListAsync();

        return new DataExportDto
        {
            DatasetName = "customers",
            RecordCount = customers.Count,
            CsvContent = ToCsv(customers)
        };
    }

    public async Task<DataExportDto> ExportTransactionsAsync(DateOnly from, DateOnly to)
    {
        var entries = await _db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.BusinessDate >= from && j.BusinessDate <= to)
            .SelectMany(j => j.Lines, (j, l) => new
            {
                j.ReferenceNumber, j.BusinessDate, j.Description,
                l.GLAccountId, l.DebitAmount, l.CreditAmount, l.LineDescription
            }).ToListAsync();

        return new DataExportDto
        {
            DatasetName = "journal_entries",
            RecordCount = entries.Count,
            CsvContent = ToCsv(entries)
        };
    }

    public async Task<PortfolioHealthDto> GetPortfolioHealthAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var loans = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .Select(l => new { l.Id, l.CurrentClassification, l.OutstandingAmount, l.ProvisionAmount, l.BranchId })
            .ToListAsync();

        var totalOutstanding = loans.Sum(l => l.OutstandingAmount);
        var nplOutstanding = loans.Where(l => l.CurrentClassification >= 3).Sum(l => l.OutstandingAmount);
        var totalProvision = loans.Sum(l => l.ProvisionAmount);

        // Collateral coverage
        var totalCollateralValue = await _db.Collaterals.Where(c => c.Status == "Active").SumAsync(c => c.AppraisedValue);

        // NPL trend from snapshots (last 30 days)
        var thirtyDaysAgo = today.AddDays(-30);
        var snapshots = await _db.ClassificationSnapshots
            .Where(s => s.SnapshotDate >= thirtyDaysAgo)
            .GroupBy(s => s.SnapshotDate)
            .Select(g => new
            {
                Date = g.Key,
                TotalOutstanding = g.Sum(s => s.OutstandingAmount),
                NplOutstanding = g.Where(s => s.Classification >= 3).Sum(s => s.OutstandingAmount)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var nplTrend = snapshots.Select(s => new NplTrendPoint
        {
            Date = s.Date,
            Outstanding = s.TotalOutstanding,
            NplRatio = s.TotalOutstanding > 0 ? Math.Round(s.NplOutstanding / s.TotalOutstanding * 100, 2) : 0
        }).ToList();

        // Branch comparison
        var branchIds = loans.Where(l => l.BranchId.HasValue).Select(l => l.BranchId!.Value).Distinct().ToList();
        var branches = await _db.Branches.Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BranchName);

        var branchComparison = loans.Where(l => l.BranchId.HasValue)
            .GroupBy(l => l.BranchId!.Value)
            .Select(g =>
            {
                var bOut = g.Sum(l => l.OutstandingAmount);
                var bNpl = g.Where(l => l.CurrentClassification >= 3).Sum(l => l.OutstandingAmount);
                return new BranchComparisonItem
                {
                    BranchId = g.Key,
                    BranchName = branches.GetValueOrDefault(g.Key, $"Branch #{g.Key}"),
                    TotalLoans = g.Count(),
                    TotalOutstanding = bOut,
                    NplLoans = g.Count(l => l.CurrentClassification >= 3),
                    NplRatio = bOut > 0 ? Math.Round(bNpl / bOut * 100, 2) : 0
                };
            }).ToList();

        return new PortfolioHealthDto
        {
            AsOfDate = today,
            NplRatio = totalOutstanding > 0 ? Math.Round(nplOutstanding / totalOutstanding * 100, 2) : 0,
            ProvisionCoverage = nplOutstanding > 0 ? Math.Round(totalProvision / nplOutstanding * 100, 2) : 0,
            TotalLoans = loans.Count,
            TotalOutstanding = totalOutstanding,
            CollateralCoverage = totalOutstanding > 0 ? Math.Round(totalCollateralValue / totalOutstanding * 100, 2) : 0,
            NplTrend = nplTrend,
            BranchComparison = branchComparison
        };
    }

    private static string ToCsv<T>(IEnumerable<T> items)
    {
        var sb = new StringBuilder();
        var properties = typeof(T).GetProperties();
        sb.AppendLine(string.Join(",", properties.Select(p => p.Name)));
        foreach (var item in items)
        {
            var values = properties.Select(p =>
            {
                var val = p.GetValue(item)?.ToString() ?? "";
                return val.Contains(',') || val.Contains('"') ? $"\"{val.Replace("\"", "\"\"")}\"" : val;
            });
            sb.AppendLine(string.Join(",", values));
        }
        return sb.ToString();
    }
}
