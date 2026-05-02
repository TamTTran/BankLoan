namespace LoanFlow.Application.Services;

public interface IInterestRateReportService
{
    Task<InterestRateReportDto> GenerateReportAsync(int year, int month);
}
