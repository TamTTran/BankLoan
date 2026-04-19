using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/reports/npl")]
[Authorize]
public class NplReportsController : ControllerBase
{
    private readonly INplReportingService _nplService;

    public NplReportsController(INplReportingService nplService) => _nplService = nplService;

    /// <summary>NPL Summary: ratio, provision coverage, group breakdown</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateOnly? asOfDate)
    {
        var result = await _nplService.GetNplSummaryAsync(asOfDate);
        return Ok(result);
    }

    /// <summary>Migration Matrix: month-over-month group transitions</summary>
    [HttpGet("migration")]
    public async Task<IActionResult> GetMigration([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (from >= to) return BadRequest("'from' must be before 'to'.");
        var result = await _nplService.GetMigrationMatrixAsync(from, to);
        return Ok(result);
    }

    /// <summary>NPL Breakdown by product type</summary>
    [HttpGet("by-product")]
    public async Task<IActionResult> GetByProduct([FromQuery] DateOnly? asOfDate)
    {
        var result = await _nplService.GetNplByProductAsync(asOfDate);
        return Ok(result);
    }

    /// <summary>NPL Breakdown by loan officer</summary>
    [HttpGet("by-officer")]
    public async Task<IActionResult> GetByOfficer([FromQuery] DateOnly? asOfDate)
    {
        var result = await _nplService.GetNplByOfficerAsync(asOfDate);
        return Ok(result);
    }
}
