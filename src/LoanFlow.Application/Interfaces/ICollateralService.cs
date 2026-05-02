using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Collateral;

namespace LoanFlow.Application.Services;

public interface ICollateralService
{
    Task<IEnumerable<CollateralDto>> GetByCustomerIdAsync(int customerId);
    Task<Result<CollateralDto>> GetByIdAsync(int id);
    Task<Result<CollateralDto>> CreateAsync(CreateCollateralRequest request);
    Task<Result<CollateralDto>> UpdateAsync(int id, UpdateCollateralRequest request);
    
    // Loan attachment
    Task<IEnumerable<LoanCollateralDto>> GetByLoanIdAsync(int loanId);
    Task<Result> AllocateToLoanAsync(int loanId, AllocateCollateralRequest request);
    Task<Result> RemoveFromLoanAsync(int loanId, int collateralId);
}
