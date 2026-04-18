using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize(Roles = "Admin")]
public class JobsController : ControllerBase
{
    private readonly IOverdueJobService _overdueJobService;
    private readonly IEndOfDayService _eodService;
    private readonly IBusinessCalendarService _businessCalendar;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public JobsController(IOverdueJobService overdueJobService, IEndOfDayService eodService, IBusinessCalendarService businessCalendar)
    {
        _overdueJobService = overdueJobService;
        _eodService = eodService;
        _businessCalendar = businessCalendar;
    }

    /// <summary>
    /// POST /api/jobs/overdue/run — Manually trigger the daily overdue processing job.
    /// Useful for catch-up, testing, or re-running after a failure.
    /// </summary>
    [HttpPost("overdue/run")]
    public async Task<IActionResult> RunOverdueJob([FromQuery] DateOnly? date, [FromQuery] int batchSize = 100)
    {
        var businessDate = date ?? _businessCalendar.GetCurrentBusinessDate();

        var result = await _overdueJobService.RunDailyOverdueProcessingAsync(businessDate, batchSize);

        return Ok(result);
    }

    /// <summary>
    /// GET /api/jobs/overdue/history — Query snapshot summary by date range.
    /// </summary>
    [HttpGet("overdue/history")]
    public async Task<IActionResult> GetOverdueHistory([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var dateTo = to ?? _businessCalendar.GetCurrentBusinessDate();
        var dateFrom = from ?? dateTo.AddDays(-30);

        var history = await _overdueJobService.GetSnapshotHistoryAsync(dateFrom, dateTo);

        return Ok(history);
    }

    /// <summary>
    /// POST /api/jobs/eod — Run the full End-of-Day processing pipeline.
    /// Includes: Overdue aging → Late fees → Reconciliation → Portfolio summary.
    /// </summary>
    [HttpPost("eod")]
    public async Task<IActionResult> RunEndOfDay([FromQuery] DateOnly? date)
    {
        var businessDate = date ?? _businessCalendar.GetCurrentBusinessDate();

        var report = await _eodService.RunAsync(businessDate, CurrentUserId);

        return Ok(new { success = true, data = report });
    }
}
