using LoanFlow.Application.DTOs.Dashboard;

namespace LoanFlow.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<OverdueBucketReportDto> GetOverdueBucketReportAsync();
    Task<MonthlyTrendDto> GetMonthlyTrendAsync(int months = 6);
}
