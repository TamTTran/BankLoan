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
