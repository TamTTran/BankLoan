namespace LoanFlow.Application.Services;

public interface ITransactionMonitoringService
{
    Task MonitorDailyTransactionsAsync(DateOnly date);
}
