namespace LoanFlow.Application.Services;

public interface ILiquidityReportService
{
    Task<LiquidityReportDto> GenerateReportAsync(int year, int month);
}
