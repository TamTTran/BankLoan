namespace LoanFlow.Application.Services;

public interface ICapitalAdequacyService
{
    Task<CarReportDto> CalculateCarAsync(int year, int month);
}
