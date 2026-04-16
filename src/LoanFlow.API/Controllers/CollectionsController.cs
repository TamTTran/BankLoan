using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _service;

    public CollectionsController(ICollectionService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";

    /// <summary>GET /collections/queue — Collector's own overdue queue</summary>
    [HttpGet("collections/queue")]
    [Authorize(Roles = $"{Roles.Collector},{Roles.Admin}")]
    public async Task<IActionResult> GetMyQueue()
    {
        var data = CurrentRole == Roles.Admin
            ? await _service.GetAllCollectionQueueAsync()
            : await _service.GetCollectorQueueAsync(CurrentUserId);
        return Ok(new { success = true, data });
    }

    /// <summary>GET /collections/queue/all — All overdue loans (Admin, Manager)</summary>
    [HttpGet("collections/queue/all")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> GetAllQueue()
    {
        var data = await _service.GetAllCollectionQueueAsync();
        return Ok(new { success = true, data });
    }

    /// <summary>POST /loans/{id}/assign-collector</summary>
    [HttpPost("loans/{id:int}/assign-collector")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> AssignCollector(int id, [FromBody] AssignCollectorDto request)
    {
        var result = await _service.AssignCollectorAsync(id, request.CollectorUserId, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Collector assigned." } });
    }

    /// <summary>POST /loans/{id}/collection-actions — Log structured collection action</summary>
    [HttpPost("loans/{id:int}/collection-actions")]
    [Authorize(Roles = $"{Roles.Collector},{Roles.Admin}")]
    public async Task<IActionResult> AddCollectionAction(int id, [FromBody] CollectionActionDto request)
    {
        var result = await _service.AddCollectionActionAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Collection action recorded." } });
    }

    /// <summary>GET /loans/{id}/collection-actions — Collection history timeline</summary>
    [HttpGet("loans/{id:int}/collection-actions")]
    public async Task<IActionResult> GetCollectionHistory(int id)
    {
        var data = await _service.GetCollectionHistoryAsync(id);
        return Ok(new { success = true, data });
    }

    /// <summary>GET /collections/summary — Dashboard KPIs</summary>
    [HttpGet("collections/summary")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> GetSummary()
    {
        var data = await _service.GetCollectionSummaryAsync();
        return Ok(new { success = true, data });
    }
}
