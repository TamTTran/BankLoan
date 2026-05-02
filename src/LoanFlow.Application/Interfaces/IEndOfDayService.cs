using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IEndOfDayService
{
    Task<EodReportDto> RunAsync(DateOnly businessDate, int? userId = null, CancellationToken ct = default);
}
