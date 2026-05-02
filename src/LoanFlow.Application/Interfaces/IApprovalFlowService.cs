using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.ApprovalFlow;

namespace LoanFlow.Application.Services;

public interface IApprovalFlowService
{
    Task<Result<ApprovalRequestResponse>> SubmitRequestAsync(SubmitApprovalRequest request, int userId);
    Task<IEnumerable<ApprovalRequestResponse>> GetPendingRequestsAsync();
    Task<Result<ApprovalRequestResponse>> ApproveRequestAsync(int id, int checkerUserId);
    Task<Result<ApprovalRequestResponse>> RejectRequestAsync(int id, ProcessApprovalRequest request, int checkerUserId);
}
