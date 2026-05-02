using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IReversalService
{
    /// <summary>Step 1: Request a payment reversal (creates Pending record).</summary>
    Task<Result<ReversalRequestDto>> RequestPaymentReversalAsync(int loanId, RequestReversalDto request, int userId);

    /// <summary>Step 2: Approve a pending reversal (executes the compensating transaction).</summary>
    Task<Result<ReversalRequestDto>> ApproveReversalAsync(int reversalId, int approverUserId);

    /// <summary>Step 2 (alt): Reject a pending reversal.</summary>
    Task<Result> RejectReversalAsync(int reversalId, string reason, int approverUserId);

    /// <summary>Direct fee adjustment (single-step, Admin/Approver only).</summary>
    Task<Result> AdjustFeeAsync(int loanId, AdjustFeeDto request, int userId);

    /// <summary>List reversal requests for a loan.</summary>
    Task<IEnumerable<ReversalRequestDto>> GetReversalsForLoanAsync(int loanId);

    /// <summary>List all pending reversal requests.</summary>
    Task<IEnumerable<ReversalRequestDto>> GetPendingReversalsAsync();
}
