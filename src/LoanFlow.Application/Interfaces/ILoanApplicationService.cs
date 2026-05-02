using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanApplication;
using LoanFlow.Application.DTOs.LoanProduct;

namespace LoanFlow.Application.Services;

public interface ILoanApplicationService
{
    Task<PagedResult<LoanApplicationDto>> GetListAsync(ApplicationFilterRequest filter);
    Task<Result<LoanApplicationDto>> GetByIdAsync(int id);
    Task<Result<LoanApplicationDto>> CreateAsync(CreateApplicationRequest request, int userId);
    Task<Result<LoanApplicationDto>> UpdateAsync(int id, UpdateApplicationRequest request, int userId);
    Task<Result> SubmitAsync(int id, int userId);
    Task<Result> ApproveAsync(int id, int approverUserId);
    Task<Result> RejectAsync(int id, RejectApplicationRequest request, int approverUserId);
    Task<Result<InstallmentPreviewDto>> PreviewAsync(InstallmentPreviewRequest request);
}
