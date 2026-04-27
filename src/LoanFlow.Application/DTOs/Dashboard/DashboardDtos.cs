namespace LoanFlow.Application.DTOs.Dashboard;

// GET /dashboard/summary (§17.10 / RPT-001)
public class DashboardSummaryDto
{
    public int TotalActiveLoans { get; set; }
    public int TotalOverdueLoans { get; set; }
    public decimal DueTodayAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public int PendingApprovals { get; set; }
    public List<LoanStatusSummaryDto> LoanStatusBreakdown { get; set; } = [];
}

// GET /reports/overdue-buckets (§17.10 / RPT-002)
public class OverdueBucketReportDto
{
    public List<OverdueBucketDto> Buckets { get; set; } = [];
}

public class OverdueBucketDto
{
    public string Bucket { get; set; } = default!;
    public string Label { get; set; } = default!;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class LoanStatusSummaryDto
{
    public string Status { get; set; } = default!;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

// GET /dashboard/monthly-trend — RPT-003
public class MonthlyTrendDto
{
    public List<MonthlyTrendPointDto> Points { get; set; } = [];
}

public class MonthlyTrendPointDto
{
    public string Month { get; set; } = default!;  // "YYYY-MM"
    public string Label { get; set; } = default!;  // "Jan 25"
    public decimal Disbursed { get; set; }
    public decimal Collected { get; set; }
    public int NewLoans { get; set; }
}
