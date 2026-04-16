using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/otp")]
public class OtpController : ControllerBase
{
    private readonly IEmailService _service;

    public OtpController(IEmailService service)
    {
        _service = service;
    }

    public class SendOtpReq { public string Email { get; set; } = ""; public string Purpose { get; set; } = ""; public string? DeeplinkUrl { get; set; } }
    public class VerifyOtpReq { public string Code { get; set; } = ""; }

    [HttpPost("send")]
    [Authorize]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpReq req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.SendOtpAsync(userId, req.Email, req.Purpose, req.DeeplinkUrl);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "OTP sent successfully." } }); // Optionally return deeplinkToken in data for testing
    }

    [HttpPost("verify")]
    [Authorize]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpReq req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.VerifyOtpAsync(userId, req.Code);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "OTP verified successfully." } });
    }

    [HttpGet("deeplink/{token}")]
    [Authorize]
    public async Task<IActionResult> VerifyDeeplink(string token)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.VerifyOtpAsync(userId, "", token);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Deeplink verified successfully." } });
    }
}
