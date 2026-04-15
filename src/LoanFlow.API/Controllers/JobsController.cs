using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize(Roles = "Admin")]
public class JobsController : ControllerBase
{
    private readonly IOverdueJobService _overdueJobService;

    public JobsController(IOverdueJobService overdueJobService) => _overdueJobService = overdueJobService;

    /// <summary>
    /// POST /api/jobs/overdue/run — Manually trigger the daily overdue processing job.
    /// Useful for catch-up, testing, or re-running after a failure.
    /// </summary>
    [HttpPost("overdue/run")]
    public async Task<IActionResult> RunOverdueJob([FromQuery] DateOnly? date, [FromQuery] int batchSize = 100)
    {
        // Default to today's business date (UTC+7)
        var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var businessDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        var result = await _overdueJobService.RunDailyOverdueProcessingAsync(businessDate, batchSize);

        return Ok(result);
    }

    /// <summary>
    /// GET /api/jobs/overdue/history — Query snapshot summary by date range.
    /// </summary>
    [HttpGet("overdue/history")]
    public async Task<IActionResult> GetOverdueHistory([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var history = await _overdueJobService.GetSnapshotHistoryAsync(dateFrom, dateTo);

        return Ok(history);
    }
}
