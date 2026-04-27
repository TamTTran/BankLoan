using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

/// <summary>
/// Exposes Capital Adequacy Ratio (CAR) data at the route the frontend expects.
/// The calculation is delegated to ICapitalAdequacyService.
/// </summary>
[ApiController]
[Route("api/capital-adequacy")]
[Authorize(Roles = "Admin,Manager,RiskOfficer")]
public class CapitalAdequacyController : ControllerBase
{
    private readonly ICapitalAdequacyService _carService;

    public CapitalAdequacyController(ICapitalAdequacyService carService)
    {
        _carService = carService;
    }

    /// <summary>Latest CAR snapshot (current month)</summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        var now = DateTime.UtcNow;
        var report = await _carService.CalculateCarAsync(now.Year, now.Month);
        return Ok(report);
    }

    /// <summary>CAR for a specific month</summary>
    [HttpGet]
    public async Task<IActionResult> GetByMonth([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _carService.CalculateCarAsync(year, month);
        return Ok(report);
    }
}
