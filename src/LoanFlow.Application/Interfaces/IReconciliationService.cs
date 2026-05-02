using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IReconciliationService
{
    Task<ReconciliationReportDto> RunReconciliationAsync(DateOnly businessDate, int? userId);
    Task<PagedResult<ReconciliationExceptionDto>> GetExceptionsAsync(string? status, int page = 1, int pageSize = 50);
    Task<Result> ResolveExceptionAsync(int exceptionId, ResolveExceptionDto request, int userId);
}
