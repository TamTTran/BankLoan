namespace LoanFlow.Application.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public int TotalActiveLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalDisbursedThisMonth { get; set; }
    public int NewApplicationsThisMonth { get; set; }
    public int PendingApprovals { get; set; }
    public int OverdueAccounts { get; set; }
    public decimal CollectedThisMonth { get; set; }
    public List<OverdueBucketDto> OverdueBuckets { get; set; } = [];
    public List<LoanStatusSummaryDto> LoanStatusBreakdown { get; set; } = [];
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

public class OverdueItemDto
{
    public int LoanId { get; set; }
    public string LoanNumber { get; set; } = default!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    public decimal OutstandingBalance { get; set; }
    public decimal OverdueAmount { get; set; }
    public int DaysOverdue { get; set; }
    public string Bucket { get; set; } = default!;
    public string? LastCollectionNote { get; set; }
    public string? AssignedCollector { get; set; }
}
