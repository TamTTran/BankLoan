using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var data = await _service.GetUserNotificationsAsync(CurrentUserId, unreadOnly, page, pageSize);
        return Ok(new { success = true, data });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _service.GetUnreadCountAsync(CurrentUserId);
        return Ok(new { success = true, data = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var result = await _service.MarkAsReadAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Notification marked as read." } });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _service.MarkAllAsReadAsync(CurrentUserId);
        return Ok(new { success = true, data = new { message = "All notifications marked as read." } });
    }
}

