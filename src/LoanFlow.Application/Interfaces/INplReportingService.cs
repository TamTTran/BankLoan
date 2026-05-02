namespace LoanFlow.Application.Services;

public interface INplReportingService
{
    Task<NplSummaryDto> GetNplSummaryAsync(DateOnly? asOfDate);
    Task<MigrationMatrixDto> GetMigrationMatrixAsync(DateOnly fromMonth, DateOnly toMonth);
    Task<NplByDimensionDto> GetNplByProductAsync(DateOnly? asOfDate);
    Task<NplByDimensionDto> GetNplByOfficerAsync(DateOnly? asOfDate);
}
