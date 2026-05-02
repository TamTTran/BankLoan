namespace LoanFlow.Application.Services;

public interface IFinancialStatementService
{
    Task<FinancialReportDto> GenerateBalanceSheetAsync(DateOnly asOfDate);
    Task<FinancialReportDto> GenerateProfitAndLossAsync(DateOnly fromDate, DateOnly toDate);
}
