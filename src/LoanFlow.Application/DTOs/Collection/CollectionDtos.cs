using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class CollectionActionDto
{
    public string ActionType { get; set; } = "";
    public DateOnly ActionDate { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string Outcome { get; set; } = "";
    public DateOnly? PromiseDate { get; set; }
    public decimal? PromiseAmount { get; set; }
    public string? Notes { get; set; }
}

public class CollectionActionItemDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string CollectorName { get; set; } = "";
    public string ActionType { get; set; } = "";
    public DateOnly ActionDate { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string Outcome { get; set; } = "";
    public DateOnly? PromiseDate { get; set; }
    public decimal? PromiseAmount { get; set; }
    public string? PromiseStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CollectionQueueItemDto
{
    public int LoanId { get; set; }
    public string LoanAccountNo { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public decimal OutstandingAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int Dpd { get; set; }
    public string Bucket { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? AssignedCollector { get; set; }
    public int? AssignmentId { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public DateOnly? LastPromiseDate { get; set; }
    public string? LastPromiseStatus { get; set; }
    public string? LastActionOutcome { get; set; }
    public int TotalActions { get; set; }
}

public class CollectionSummaryDto
{
    public int TotalOverdueLoans { get; set; }
    public int TotalAssigned { get; set; }
    public int TotalUnassigned { get; set; }
    public int ActionsToday { get; set; }
    public int PromisesPending { get; set; }
    public int PromisesBroken { get; set; }
    public decimal TotalOverdueAmount { get; set; }
}

public class AssignCollectorDto
{
    public int CollectorUserId { get; set; }
}
