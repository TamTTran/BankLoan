using System;

namespace LoanFlow.Application.DTOs.ApprovalFlow;

public class SubmitApprovalRequest
{
    public string ActionType { get; set; } = default!;
    public string Payload { get; set; } = "{}";
}

public class ApprovalRequestResponse
{
    public int Id { get; set; }
    public string ActionType { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class ProcessApprovalRequest
{
    public string? RejectionReason { get; set; }
}
