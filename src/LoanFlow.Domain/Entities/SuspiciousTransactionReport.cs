namespace LoanFlow.Domain.Entities;

public class SuspiciousTransactionReport
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string StrNumber { get; set; } = default!; // Unique identifier for the report
    
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>Structuring | LargeCash | RapidMovement | UnusualPattern</summary>
    public string SuspicionType { get; set; } = default!;
    
    public string Description { get; set; } = default!;
    
    /// <summary>JSON array of related transaction IDs</summary>
    public string RelatedTransactionIdsJson { get; set; } = "[]";
    
    /// <summary>Draft | PendingApproval | SubmittedToNhnn | Rejected</summary>
    public string Status { get; set; } = "Draft";
    
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public Customer Customer { get; set; } = default!;
}
