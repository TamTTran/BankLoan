using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IBranchService
{
    Task<List<BranchDto>> GetAllAsync();
    Task<Result<BranchDto>> CreateAsync(CreateBranchRequest req);
    Task<Result> UpdateAsync(int id, UpdateBranchRequest req);
    Task<BranchPortfolioDto?> GetBranchPortfolioAsync(int branchId);
}
