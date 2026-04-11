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
}
