using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── NPL Summary DTO ─────────────────────────────────────────────────────────
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

// ── Migration Matrix DTO ────────────────────────────────────────────────────
public class MigrationMatrixDto
{
    public DateOnly FromMonth { get; set; }
    public DateOnly ToMonth { get; set; }
    public List<MigrationMatrixRow> Rows { get; set; } = [];
    public int TotalLoansAnalyzed { get; set; }
}

public class MigrationMatrixRow
{
    public int FromGroup { get; set; }
    public int ToGroup { get; set; }
    public int LoanCount { get; set; }
    public decimal TotalOutstanding { get; set; }
    public string Direction { get; set; } = "";     // "Stable" | "Upgraded" | "Downgraded"
}

// ── NPL By Product / Officer DTO ────────────────────────────────────────────
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

// ── Interface ───────────────────────────────────────────────────────────────
public interface INplReportingService
{
    Task<NplSummaryDto> GetNplSummaryAsync(DateOnly? asOfDate);
    Task<MigrationMatrixDto> GetMigrationMatrixAsync(DateOnly fromMonth, DateOnly toMonth);
    Task<NplByDimensionDto> GetNplByProductAsync(DateOnly? asOfDate);
    Task<NplByDimensionDto> GetNplByOfficerAsync(DateOnly? asOfDate);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class NplReportingService : INplReportingService
{
    private readonly LoanFlowDbContext _db;

    private static readonly Dictionary<int, string> GroupNames = new()
    {
        { 1, "Nợ đủ tiêu chuẩn" },
        { 2, "Nợ cần chú ý" },
        { 3, "Nợ dưới tiêu chuẩn" },
        { 4, "Nợ nghi ngờ" },
        { 5, "Nợ có khả năng mất vốn" }
    };

    private static readonly Dictionary<int, decimal> ProvisionRates = new()
    {
        { 1, 0m }, { 2, 5m }, { 3, 20m }, { 4, 50m }, { 5, 100m }
    };

    public NplReportingService(LoanFlowDbContext db) => _db = db;

    public async Task<NplSummaryDto> GetNplSummaryAsync(DateOnly? asOfDate)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var loans = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .Select(l => new { l.CurrentClassification, l.OutstandingAmount, l.ProvisionAmount })
            .ToListAsync();

        var totalOutstanding = loans.Sum(l => l.OutstandingAmount);
        var nplOutstanding = loans.Where(l => l.CurrentClassification >= 3).Sum(l => l.OutstandingAmount);

        var groups = Enumerable.Range(1, 5).Select(g =>
        {
            var groupLoans = loans.Where(l => l.CurrentClassification == g).ToList();
            return new ClassificationGroupDto
            {
                Group = g,
                GroupName = GroupNames.GetValueOrDefault(g, $"Group {g}"),
                LoanCount = groupLoans.Count,
                Outstanding = groupLoans.Sum(l => l.OutstandingAmount),
                Provision = groupLoans.Sum(l => l.ProvisionAmount),
                ProvisionRate = ProvisionRates.GetValueOrDefault(g, 0),
                ShareOfPortfolio = totalOutstanding > 0
                    ? Math.Round(groupLoans.Sum(l => l.OutstandingAmount) / totalOutstanding * 100, 2)
                    : 0
            };
        }).ToList();

        return new NplSummaryDto
        {
            AsOfDate = date,
            TotalActiveLoans = loans.Count,
            TotalOutstanding = totalOutstanding,
            NplOutstanding = nplOutstanding,
            NplRatio = totalOutstanding > 0 ? Math.Round(nplOutstanding / totalOutstanding * 100, 2) : 0,
            TotalProvision = loans.Sum(l => l.ProvisionAmount),
            ProvisionCoverageRatio = nplOutstanding > 0
                ? Math.Round(loans.Sum(l => l.ProvisionAmount) / nplOutstanding * 100, 2)
                : 0,
            Groups = groups
        };
    }

    public async Task<MigrationMatrixDto> GetMigrationMatrixAsync(DateOnly fromMonth, DateOnly toMonth)
    {
        // Get last snapshot of each month for each loan
        var fromEnd = fromMonth.AddMonths(1).AddDays(-1);
        var toEnd = toMonth.AddMonths(1).AddDays(-1);

        var fromSnapshots = await _db.Set<ClassificationSnapshot>()
            .Where(s => s.SnapshotDate >= fromMonth && s.SnapshotDate <= fromEnd)
            .GroupBy(s => s.LoanId)
            .Select(g => new
            {
                LoanId = g.Key,
                Classification = g.OrderByDescending(s => s.SnapshotDate).First().Classification,
                Outstanding = g.OrderByDescending(s => s.SnapshotDate).First().OutstandingAmount
            })
            .ToListAsync();

        var toSnapshots = await _db.Set<ClassificationSnapshot>()
            .Where(s => s.SnapshotDate >= toMonth && s.SnapshotDate <= toEnd)
            .GroupBy(s => s.LoanId)
            .Select(g => new
            {
                LoanId = g.Key,
                Classification = g.OrderByDescending(s => s.SnapshotDate).First().Classification,
                Outstanding = g.OrderByDescending(s => s.SnapshotDate).First().OutstandingAmount
            })
            .ToListAsync();

        var fromDict = fromSnapshots.ToDictionary(s => s.LoanId);
        var toDict = toSnapshots.ToDictionary(s => s.LoanId);

        // Only analyze loans present in both months
        var commonLoanIds = fromDict.Keys.Intersect(toDict.Keys).ToList();

        var rows = commonLoanIds
            .GroupBy(id => new { From = fromDict[id].Classification, To = toDict[id].Classification })
            .Select(g => new MigrationMatrixRow
            {
                FromGroup = g.Key.From,
                ToGroup = g.Key.To,
                LoanCount = g.Count(),
                TotalOutstanding = g.Sum(id => toDict[id].Outstanding),
                Direction = g.Key.From == g.Key.To ? "Stable"
                          : g.Key.To < g.Key.From ? "Upgraded"
                          : "Downgraded"
            })
            .OrderBy(r => r.FromGroup)
            .ThenBy(r => r.ToGroup)
            .ToList();

        return new MigrationMatrixDto
        {
            FromMonth = fromMonth,
            ToMonth = toMonth,
            Rows = rows,
            TotalLoansAnalyzed = commonLoanIds.Count
        };
    }

    public async Task<NplByDimensionDto> GetNplByProductAsync(DateOnly? asOfDate)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var loans = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .Select(l => new { l.ProductType, l.CurrentClassification, l.OutstandingAmount, l.ProvisionAmount })
            .ToListAsync();

        var items = loans
            .GroupBy(l => l.ProductType)
            .Select(g =>
            {
                var nplLoans = g.Where(l => l.CurrentClassification >= 3).ToList();
                var totalOut = g.Sum(l => l.OutstandingAmount);
                var nplOut = nplLoans.Sum(l => l.OutstandingAmount);
                return new NplDimensionItem
                {
                    DimensionKey = g.Key,
                    TotalLoans = g.Count(),
                    TotalOutstanding = totalOut,
                    NplLoans = nplLoans.Count,
                    NplOutstanding = nplOut,
                    NplRatio = totalOut > 0 ? Math.Round(nplOut / totalOut * 100, 2) : 0,
                    Provision = g.Sum(l => l.ProvisionAmount)
                };
            }).ToList();

        return new NplByDimensionDto { AsOfDate = date, Items = items };
    }

    public async Task<NplByDimensionDto> GetNplByOfficerAsync(DateOnly? asOfDate)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var loans = await _db.Loans
            .Include(l => l.Customer)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .Select(l => new
            {
                OfficerId = l.Customer.AssignedToUserId,
                l.CurrentClassification,
                l.OutstandingAmount,
                l.ProvisionAmount
            })
            .ToListAsync();

        // Fetch officer names
        var officerIds = loans.Select(l => l.OfficerId).Distinct().ToList();
        var officers = await _db.Users
            .Where(u => officerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var items = loans
            .GroupBy(l => l.OfficerId)
            .Select(g =>
            {
                var nplLoans = g.Where(l => l.CurrentClassification >= 3).ToList();
                var totalOut = g.Sum(l => l.OutstandingAmount);
                var nplOut = nplLoans.Sum(l => l.OutstandingAmount);
                return new NplDimensionItem
                {
                    DimensionKey = officers.GetValueOrDefault(g.Key, $"Officer #{g.Key}"),
                    TotalLoans = g.Count(),
                    TotalOutstanding = totalOut,
                    NplLoans = nplLoans.Count,
                    NplOutstanding = nplOut,
                    NplRatio = totalOut > 0 ? Math.Round(nplOut / totalOut * 100, 2) : 0,
                    Provision = g.Sum(l => l.ProvisionAmount)
                };
            }).ToList();

        return new NplByDimensionDto { AsOfDate = date, Items = items };
    }
}
