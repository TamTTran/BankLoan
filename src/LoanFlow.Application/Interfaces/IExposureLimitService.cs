using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IExposureLimitService
{
    Task<Result<ExposureLimitDto>> SetLimitAsync(int customerId, decimal maxExposure, int userId);
    Task<ExposureLimitDto?> GetLimitAsync(int customerId);
    Task<Result> CheckExposureAsync(int customerId, decimal additionalAmount);
    Task<Result> OverrideExposureAsync(int customerId, string reason, int userId);
    Task RefreshExposureAsync(int customerId);
}
