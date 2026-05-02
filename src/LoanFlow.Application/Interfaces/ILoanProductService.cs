using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanProduct;

namespace LoanFlow.Application.Services;

public interface ILoanProductService
{
    Task<IEnumerable<LoanProductDto>> GetAllAsync();
    Task<Result<LoanProductDto>> GetByIdAsync(int id);
    Task<Result<LoanProductDto>> CreateAsync(CreateLoanProductRequest request);
    Task<Result<LoanProductDto>> UpdateAsync(int id, UpdateLoanProductRequest request);
    Task<Result<InstallmentPreviewDto>> PreviewInstallmentAsync(InstallmentPreviewRequest request);
}
