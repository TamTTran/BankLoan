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
    private const string RefreshTokenCookieName = "refreshToken";

    public AuthController(IAuthService service) => _service = service;

    // ── Cookie helpers ───────────────────────────────────────────────────────

    private void SetRefreshTokenCookie(string plainToken)
    {
        Response.Cookies.Append(RefreshTokenCookieName, plainToken, new CookieOptions
        {
            HttpOnly = true,                     // not accessible to JS
            Secure   = true,                     // HTTPS only
            SameSite = SameSiteMode.Strict,      // CSRF protection
            Expires  = DateTimeOffset.UtcNow.AddDays(7),
            Path     = "/api/auth"               // scope to auth routes only
        });
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/api/auth"
        });
    }

    private string? ReadRefreshTokenCookie() =>
        Request.Cookies.TryGetValue(RefreshTokenCookieName, out var val) ? val : null;

    // ── Endpoints ────────────────────────────────────────────────────────────

    /// <summary>POST /auth/login — AUTH-001</summary>
    [HttpPost("login")]
    [EnableRateLimiting("LoginRateLimit")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        if (!result.IsSuccess)
            return Unauthorized(new { success = false, error = new { code = "AUTH-001", message = result.Error } });

        // RefreshToken → HttpOnly cookie; AccessToken → JSON body
        SetRefreshTokenCookie(result.Value.RefreshToken);

        var publicResponse = new PublicLoginResponse(
            result.Value.AccessToken, result.Value.Username, result.Value.FullName,
            result.Value.Role, result.Value.UserId, result.Value.ExpiresAt);

        return Ok(new { success = true, data = publicResponse });
    }

    /// <summary>POST /auth/refresh — AUTH-002 (token via HttpOnly cookie)</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("RefreshRateLimit")]
    public async Task<IActionResult> Refresh()
    {
        var plainToken = ReadRefreshTokenCookie();
        if (string.IsNullOrEmpty(plainToken))
            return Unauthorized(new { success = false, error = new { code = "AUTH-002", message = "Refresh token missing." } });

        var result = await _service.RefreshAsync(plainToken);
        if (!result.IsSuccess)
        {
            ClearRefreshTokenCookie();
            return Unauthorized(new { success = false, error = new { code = "AUTH-002", message = result.Error } });
        }

        SetRefreshTokenCookie(result.Value.RefreshToken);

        var publicResponse = new PublicLoginResponse(
            result.Value.AccessToken, result.Value.Username, result.Value.FullName,
            result.Value.Role, result.Value.UserId, result.Value.ExpiresAt);

        return Ok(new { success = true, data = publicResponse });
    }

    /// <summary>POST /auth/logout — AUTH-003 (revokes cookie token, scoped to caller)</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var plainToken = ReadRefreshTokenCookie();
        if (!string.IsNullOrEmpty(plainToken))
            await _service.LogoutAsync(userId, plainToken);

        ClearRefreshTokenCookie();
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
        ClearRefreshTokenCookie();
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var sessions = await _service.GetSessionsAsync(userId);
        return Ok(new { success = true, data = sessions });
    }

    /// <summary>POST /auth/change-password</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var result = await _service.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error = new { message = result.Error } });
        }

        ClearRefreshTokenCookie(); // Force re-login
        return Ok(new { success = true, message = "Password changed successfully. Please login again." });
    }

    [HttpPost("verify-mfa")]
    [AllowAnonymous]
    [EnableRateLimiting("LoginRateLimit")]
    public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest request)
    {
        var result = await _service.VerifyMfaAsync(request, null, null);
        if (!result.IsSuccess)
            return Unauthorized(new { success = false, error = new { message = result.Error } });

        SetRefreshTokenCookie(result.Value.RefreshToken);

        var publicResponse = new PublicLoginResponse(
            result.Value.AccessToken, result.Value.Username, result.Value.FullName,
            result.Value.Role, result.Value.UserId, result.Value.ExpiresAt);

        return Ok(new { success = true, data = publicResponse });
    }

    [HttpGet("mfa-setup")]
    [Authorize]
    public async Task<IActionResult> SetupMfa()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var result = await _service.GetMfaSetupAsync(userId);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("enable-mfa")]
    [Authorize]
    public async Task<IActionResult> EnableMfa([FromBody] EnableMfaRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var result = await _service.EnableMfaAsync(userId, request.TotpCode);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, message = "MFA enabled successfully." });
    }
}
