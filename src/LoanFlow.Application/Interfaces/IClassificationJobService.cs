using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IClassificationJobService
{
    Task RunDailyClassificationAsync(DateOnly businessDate, CancellationToken ct = default);
}
