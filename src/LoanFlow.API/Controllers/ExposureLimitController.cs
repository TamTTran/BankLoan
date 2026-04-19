using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/exposure")]
[Authorize]
public class ExposureLimitController : ControllerBase
{
    private readonly IExposureLimitService _service;
    public ExposureLimitController(IExposureLimitService service) => _service = service;

    [HttpGet("{customerId}")]
    public async Task<IActionResult> Get(int customerId)
    {
        var result = await _service.GetLimitAsync(customerId);
        return result != null ? Ok(result) : NotFound("No exposure limit set.");
    }

    [HttpPost("{customerId}")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> Set(int customerId, [FromBody] SetExposureLimitRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.SetLimitAsync(customerId, req.MaxExposure, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{customerId}/override")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Override(int customerId, [FromBody] OverrideRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.OverrideExposureAsync(customerId, req.Reason, userId);
        return result.IsSuccess ? Ok(new { message = "Exposure limit overridden." }) : BadRequest(result.Error);
    }

    [HttpPost("{customerId}/refresh")]
    public async Task<IActionResult> Refresh(int customerId)
    {
        await _service.RefreshExposureAsync(customerId);
        return Ok(new { message = "Exposure refreshed." });
    }

    public class OverrideRequest { public string Reason { get; set; } = ""; }
}
