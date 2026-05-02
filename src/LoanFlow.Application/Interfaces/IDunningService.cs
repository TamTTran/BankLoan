namespace LoanFlow.Application.Services;

public interface IDunningService
{
    Task<List<DunningStrategyDto>> GetStrategiesAsync();
    Task<DunningStrategyDto> UpsertStrategyAsync(int dpdThreshold, string actionType, string? description, int? templateId);
    Task<List<NotificationTemplateDto>> GetTemplatesAsync();
    Task<NotificationTemplateDto> UpsertTemplateAsync(string code, string title, string body, string channel);
    Task<int> ExecuteDunningAsync(DateOnly businessDate);
}
