using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IBodService
{
    Task<Result> RunBodAsync(DateOnly newBusinessDate, int? userId = null);
}
