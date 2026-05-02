namespace LoanFlow.Application.Services;

public interface IDataExportService
{
    Task<DataExportDto> ExportLoansAsync();
    Task<DataExportDto> ExportCustomersAsync();
    Task<DataExportDto> ExportTransactionsAsync(DateOnly from, DateOnly to);
    Task<PortfolioHealthDto> GetPortfolioHealthAsync();
}
