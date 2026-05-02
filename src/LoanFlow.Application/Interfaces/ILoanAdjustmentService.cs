using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanAdjustment;

namespace LoanFlow.Application.Services;

public interface ILoanAdjustmentService
{
    Task<Result> ApplyApprovedAdjustmentAsync(int approvalRequestId, int checkerUserId);
}
