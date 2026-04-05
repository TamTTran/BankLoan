using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;
    public AuthController(IAuthService service) => _service = service;

    /// <summary>POST /auth/login — AUTH-001</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        if (!result.IsSuccess) return Unauthorized(new { success = false, error = new { code = "AUTH-001", message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }
}
