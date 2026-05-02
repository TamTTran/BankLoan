namespace LoanFlow.Application.Services;

public interface IRiskModelService
{
    Task<ExpectedLossDto> CalculateExpectedLossAsync(int loanId);
    Task<PortfolioExpectedLossDto> GetPortfolioExpectedLossAsync();
}
