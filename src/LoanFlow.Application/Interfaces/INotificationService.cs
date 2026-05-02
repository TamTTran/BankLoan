using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface INotificationService
{
    Task GenerateDueRemindersAsync(DateOnly businessDate, int daysBefore = 3);
    Task GenerateOverdueRemindersAsync(DateOnly businessDate);
    Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly, int page = 1, int pageSize = 50);
    Task<Result> MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task ProcessDeliveryQueueAsync();
    /// <summary>Instantly push an in-app notification to a specific user.</summary>
    Task SendAsync(int userId, string type, string title, string message, string? deeplinkUrl = null, int? loanId = null, int? customerId = null);
}
