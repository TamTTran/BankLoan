using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/advanced-loans")]
[Authorize(Roles = "Admin,Manager")]
public class AdvancedLendingController : ControllerBase
{
    private readonly ILoanLifecycleService _service;

    public AdvancedLendingController(ILoanLifecycleService service)
    {
        _service = service;
    }

    [HttpPost("{id}/restructure")]
    public async Task<IActionResult> Restructure(int id, [FromBody] RestructureRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.RestructureAsync(id, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id}/reschedule")]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.RescheduleAsync(id, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id}/write-off")]
    public async Task<IActionResult> WriteOff(int id, [FromBody] WriteOffRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.WriteOffAsync(id, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id}/recovery")]
    public async Task<IActionResult> Recovery(int id, [FromBody] RecordRecoveryRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.RecordRecoveryAsync(id, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
