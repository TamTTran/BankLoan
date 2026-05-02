using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class PortfolioReportDto
{
    public DateOnly AsOfDate { get; set; }
    public int TotalLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public List<StatusBreakdownItem> StatusBreakdown { get; set; } = [];
    public List<ProductBreakdownItem> ProductBreakdown { get; set; } = [];
    public decimal AvgTenorMonths { get; set; }
    public decimal AvgInterestRate { get; set; }
    public int DisbursementsThisMonth { get; set; }
    public decimal DisbursementVolumeThisMonth { get; set; }
}

public class StatusBreakdownItem
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class ProductBreakdownItem
{
    public string ProductType { get; set; } = "";
    public int Count { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class OverdueMovementReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<OverdueMovementDayDto> Days { get; set; } = [];
    public int NewOverdueCount { get; set; }
    public int CuredCount { get; set; }
}

public class OverdueMovementDayDto
{
    public DateOnly Date { get; set; }
    public int TotalOverdueLoans { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public decimal AvgDpd { get; set; }
    public Dictionary<string, int> BucketCounts { get; set; } = [];
}

public class CollectionProductivityDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<CollectorProductivityItem> Collectors { get; set; } = [];
}

public class CollectorProductivityItem
{
    public int CollectorUserId { get; set; }
    public string CollectorName { get; set; } = "";
    public int TotalAssigned { get; set; }
    public int TotalActions { get; set; }
    public int Contacted { get; set; }
    public decimal ContactRate { get; set; }
    public int PromisesMade { get; set; }
    public int PromisesKept { get; set; }
    public decimal PromiseKeepRate { get; set; }
}

public class ApprovalQueueReportDto
{
    public int TotalPending { get; set; }
    public decimal AvgDaysPending { get; set; }
    public int ApprovedThisMonth { get; set; }
    public int RejectedThisMonth { get; set; }
    public decimal ApprovalRate { get; set; }
    public List<ApproverSummaryItem> ByApprover { get; set; } = [];
}

public class ApproverSummaryItem
{
    public string ApproverName { get; set; } = "";
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
}
