namespace LoanFlow.Domain.Entities;

public class PendingApprovalRequest
{
    public int Id { get; set; }
    public string ActionType { get; set; } = default!; // "FeeWaive", "ManualAdjustment", "DisbursementCorrect", etc.
    public string Payload { get; set; } = "{}";        // JSON containing request parameters
    public int RequestedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string Status { get; set; } = "Pending";    // "Pending", "Approved", "Rejected"
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    
    public User? RequestedBy { get; set; }
    public User? ApprovedBy { get; set; }
}
