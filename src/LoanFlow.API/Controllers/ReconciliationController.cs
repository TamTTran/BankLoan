using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/reconciliation")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _service;

    public ReconciliationController(IReconciliationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>POST /reconciliation/run — Trigger reconciliation for a business date</summary>
    [HttpPost("run")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RunReconciliation([FromQuery] DateOnly businessDate)
    {
        var report = await _service.RunReconciliationAsync(businessDate, CurrentUserId);
        return Ok(new { success = true, data = report });
    }

    /// <summary>GET /reconciliation/exceptions — List reconciliation exceptions</summary>
    [HttpGet("exceptions")]
    public async Task<IActionResult> GetExceptions([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var data = await _service.GetExceptionsAsync(status, page, pageSize);
        return Ok(new { success = true, data });
    }

    /// <summary>POST /reconciliation/exceptions/{id}/resolve — Mark exception as resolved</summary>
    [HttpPost("exceptions/{id:int}/resolve")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ResolveException(int id, [FromBody] ResolveExceptionDto request)
    {
        var result = await _service.ResolveExceptionAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Exception resolved." } });
    }
}
