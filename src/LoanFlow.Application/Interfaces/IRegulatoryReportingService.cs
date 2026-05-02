namespace LoanFlow.Application.Services;

public interface IRegulatoryReportingService
{
    Task<NhnnClassificationReportDto> GenerateNhnnClassificationReportAsync(int year, int month);
    Task<NhnnProvisionReportDto> GenerateNhnnProvisionReportAsync(int year, int month);
    Task<CicPeriodicReportDto> GenerateCicPeriodicReportAsync(int year, int month);
    string ToCsv<T>(IEnumerable<T> items);
    byte[] ToExcel<T>(IEnumerable<T> items, string sheetName = "Data");
    string ToXml<T>(IEnumerable<T> items, string rootName = "Data");
}
