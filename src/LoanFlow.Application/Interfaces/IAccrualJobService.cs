using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IAccrualJobService
{
    Task RunDailyAccrualAsync(DateOnly businessDate, CancellationToken ct = default);
}
