using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ICollateralLifecycleService
{
    Task<Result<RevaluationDto>> RevalueAsync(int collateralId, RevalueRequest req, int userId);
    Task<List<RevaluationDto>> GetRevaluationHistoryAsync(int collateralId);
    Task<Result> ReleaseCollateralAsync(int collateralId, int userId);
    Task<Result> SeizeCollateralAsync(int collateralId, int userId);
}
