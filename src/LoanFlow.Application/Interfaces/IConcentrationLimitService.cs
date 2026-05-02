namespace LoanFlow.Application.Services;

public interface IConcentrationLimitService
{
    Task<ConcentrationReportDto> GetConcentrationReportAsync();
}
