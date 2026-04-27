using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    public DashboardController(IDashboardService service) => _service = service;

    /// <summary>GET /dashboard/summary — RPT-001</summary>
    [HttpGet("dashboard/summary")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Manager}")]
    public async Task<IActionResult> GetSummary()
        => Ok(new { success = true, data = await _service.GetSummaryAsync() });

    /// <summary>GET /reports/overdue-buckets — RPT-002</summary>
    [HttpGet("reports/overdue-buckets")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetOverdueBuckets()
        => Ok(new { success = true, data = await _service.GetOverdueBucketReportAsync() });

    /// <summary>GET /dashboard/monthly-trend?months=6 — RPT-003</summary>
    [HttpGet("dashboard/monthly-trend")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Manager}")]
    public async Task<IActionResult> GetMonthlyTrend([FromQuery] int months = 6)
        => Ok(new { success = true, data = await _service.GetMonthlyTrendAsync(Math.Clamp(months, 3, 12)) });
}
