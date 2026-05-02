namespace LoanFlow.Application.Services;

public interface IReportingService
{
    Task<PortfolioReportDto> GetPortfolioReportAsync(DateOnly? asOfDate);
    Task<OverdueMovementReportDto> GetOverdueMovementReportAsync(DateOnly from, DateOnly to);
    Task<CollectionProductivityDto> GetCollectionProductivityAsync(DateOnly from, DateOnly to);
    Task<ApprovalQueueReportDto> GetApprovalQueueReportAsync();
}
