using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Đăng nhập hệ thống</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (!result.IsSuccess) return Unauthorized(new { message = result.Error });
        return Ok(result.Value);
    }
}
