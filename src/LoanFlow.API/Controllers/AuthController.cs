using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;
    public AuthController(IAuthService service) => _service = service;

    /// <summary>POST /auth/login — AUTH-001</summary>
    [HttpPost("login")]
    [EnableRateLimiting("LoginRateLimit")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        if (!result.IsSuccess) return Unauthorized(new { success = false, error = new { code = "AUTH-001", message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /auth/refresh — AUTH-002</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _service.RefreshAsync(request.RefreshToken);
        if (!result.IsSuccess) return Unauthorized(new { success = false, error = new { code = "AUTH-002", message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /auth/logout — AUTH-003</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _service.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>POST /auth/logout-all — AUTH-004</summary>
    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        await _service.LogoutAllAsync(userId);
        return NoContent();
    }

    /// <summary>GET /auth/sessions — AUTH-005</summary>
    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var sessions = await _service.GetSessionsAsync(userId);
        return Ok(new { success = true, data = sessions });
    }
}
