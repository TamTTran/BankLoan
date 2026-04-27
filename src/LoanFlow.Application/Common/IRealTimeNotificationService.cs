namespace LoanFlow.Application.Common;

public interface IRealTimeNotificationService
{
    Task NotifyUserAsync(int userId, string title, string message);
}
