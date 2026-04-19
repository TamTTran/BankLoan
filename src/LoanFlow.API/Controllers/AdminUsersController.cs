using LoanFlow.Application.DTOs.Admin;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminService;
    private readonly IAuthService _authService;

    public AdminUsersController(IAdminUserService adminService, IAuthService authService)
    {
        _adminService = adminService;
        _authService = authService;
    }

    // PRD-ADM-004: User management

    [HttpGet]
    public async Task<IActionResult> GetUsers() 
        => Ok(new { success = true, data = await _adminService.GetUsersAsync() });

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _adminService.CreateUserAsync(request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromQuery] string status)
    {
        var result = await _adminService.ChangeUserStatusAsync(id, status);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "User status updated." } });
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request)
    {
        var result = await _adminService.ChangeUserRoleAsync(id, request.Role);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "User role updated." } });
    }

    // PRD-ADM-003: Lock/Unlock

    [HttpPut("{id:int}/lock")]
    public async Task<IActionResult> LockAccount(int id, [FromQuery] int minutes = 60)
    {
        var result = await _adminService.LockAccountAsync(id, minutes);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Account locked." } });
    }

    [HttpPut("{id:int}/unlock")]
    public async Task<IActionResult> UnlockAccount(int id)
    {
        var result = await _adminService.UnlockAccountAsync(id);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Account unlocked." } });
    }

    // PRD-ADM-001 & PRD-ADM-002: Session viewing and remote kick

    [HttpGet("{id:int}/sessions")]
    public async Task<IActionResult> GetUserSessions(int id)
    {
        var sessions = await _authService.GetSessionsAsync(id);
        return Ok(new { success = true, data = sessions });
    }

    [HttpDelete("{id:int}/sessions")]
    public async Task<IActionResult> RevokeAllSessions(int id)
    {
        await _authService.LogoutAllAsync(id);
        return Ok(new { success = true, data = new { message = "All sessions revoked for user." } });
    }

    [HttpDelete("{id:int}/sessions/{sessionId:int}")]
    public async Task<IActionResult> RevokeSession(int id, int sessionId)
    {
        var result = await _authService.RevokeSessionAsync(id, sessionId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Session revoked successfully." } });
    }
}
