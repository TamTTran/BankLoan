using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/bod")]
[Authorize] // Assuming we want it authorized (typically System Admin)
// [Authorize(Policy = "RequireAdmin")]
public class BodController : ControllerBase
{
    private readonly IBodService _bodService;

    public BodController(IBodService bodService)
    {
        _bodService = bodService;
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunBod([FromQuery] string newBusinessDate)
    {
        if (!DateOnly.TryParse(newBusinessDate, out var date))
        {
            return BadRequest(new { Message = "Invalid newBusinessDate format. Use YYYY-MM-DD." });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? userId = int.TryParse(userIdClaim, out var id) ? id : null;

        var result = await _bodService.RunBodAsync(date, userId);
        if (!result.IsSuccess)
        {
            return BadRequest(new { Message = result.Error });
        }

        return Ok(new { Message = $"Bank-grade BOD completed successfully for {date:yyyy-MM-dd}." });
    }
}
