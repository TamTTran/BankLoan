using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _service;

    public ReportsController(IReportingService service)
    {
        _service = service;
    }

    /// <summary>GET /reports/portfolio — Portfolio overview report</summary>
    [HttpGet("portfolio")]
    public async Task<IActionResult> GetPortfolioReport([FromQuery] DateOnly? asOfDate)
    {
        var data = await _service.GetPortfolioReportAsync(asOfDate);
        return Ok(new { success = true, data });
    }

    /// <summary>GET /reports/overdue-movement — Overdue trend over date range</summary>
    [HttpGet("overdue-movement")]
    public async Task<IActionResult> GetOverdueMovement([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var data = await _service.GetOverdueMovementReportAsync(dateFrom, dateTo);
        return Ok(new { success = true, data });
    }

    /// <summary>GET /reports/collection-productivity — Per-collector KPIs</summary>
    [HttpGet("collection-productivity")]
    public async Task<IActionResult> GetCollectionProductivity([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var data = await _service.GetCollectionProductivityAsync(dateFrom, dateTo);
        return Ok(new { success = true, data });
    }

    /// <summary>GET /reports/approval-queue — Approval pipeline metrics</summary>
    [HttpGet("approval-queue")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Approver}")]
    public async Task<IActionResult> GetApprovalQueue()
    {
        var data = await _service.GetApprovalQueueReportAsync();
        return Ok(new { success = true, data });
    }
}
