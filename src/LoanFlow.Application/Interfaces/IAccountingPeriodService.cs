using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IAccountingPeriodService
{
    Task<List<AccountingPeriodDto>> GetAllPeriodsAsync();
    Task<AccountingPeriodDto?> GetCurrentPeriodAsync();
    Task<Result> ClosePeriodAsync(int year, int month, int userId, string? notes = null);
    Task<Result> ReopenPeriodAsync(int year, int month, int userId);
    Task<bool> IsPeriodOpenAsync(DateOnly businessDate);
}
