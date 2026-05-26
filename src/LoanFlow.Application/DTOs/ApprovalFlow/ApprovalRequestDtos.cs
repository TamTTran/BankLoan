using System;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;

namespace LoanFlow.Application.DTOs.ApprovalFlow;

public class SubmitApprovalRequest
{
    public string ActionType { get; set; } = default!;
    public string Payload { get; set; } = "{}";
}

public class ApprovalRequestResponse
{
    public int    Id { get; set; }
    public string ActionType { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int    RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public int?    ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class ProcessApprovalRequest
{
    public string? RejectionReason { get; set; }
}

public class RestructureApprovalPayload
{
    public int LoanId { get; set; }

    public RestructureRequest Request { get; set; } = new();
}

public class RescheduleApprovalPayload
{
    public int LoanId { get; set; }
    public RescheduleRequest Request { get; set; } = new();
}

public class WriteOffApprovalPayload
{
    public int LoanId { get; set; }
    public WriteOffRequest Request { get; set; } = new();
}

public class LateFeeWaiveApprovalPayload
{
    public int LateFeeId { get; set; }
}

public class AdjustFeeApprovalPayload
{
    public int LoanId { get; set; }
    public AdjustFeeDto Request { get; set; } = new();
}

public class DisburseApprovalPayload
{
    public int LoanId { get; set; }
    public DisburseRequest Request { get; set; } = new();
}

public class EarlySettleApprovalPayload
{
    public int LoanId { get; set; }
    public EarlySettleRequest Request { get; set; } = new();
}

public class ApplySuspenseApprovalPayload
{
    public int SuspenseId { get; set; }
}

public class ExposureOverrideApprovalPayload
{
    public int CustomerId { get; set; }
    public string Reason { get; set; } = "";
}

public class CollateralReleaseApprovalPayload
{
    public int CollateralId { get; set; }
}

public class CollateralSeizeApprovalPayload
{
    public int CollateralId { get; set; }
}

public class AccountingPeriodCloseApprovalPayload
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Notes { get; set; }
}

public class AccountingPeriodReopenApprovalPayload
{
    public int Year { get; set; }
    public int Month { get; set; }
}
