using LoanFlow.Application.Common;
using LoanFlow.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LoanFlow.API.ExternalServices;

public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public RealTimeNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyUserAsync(int userId, string title, string message)
    {
        await _hubContext.Clients.Group($"User_{userId}")
            .SendAsync("ReceiveNotification", new { Title = title, Message = message });
    }
}
