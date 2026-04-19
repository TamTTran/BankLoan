using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/dunning")]
[Authorize(Roles = "Admin,Manager")]
public class DunningController : ControllerBase
{
    private readonly IDunningService _service;
    public DunningController(IDunningService service) => _service = service;

    [HttpGet("strategies")]
    public async Task<IActionResult> GetStrategies() => Ok(await _service.GetStrategiesAsync());

    [HttpPost("strategies")]
    public async Task<IActionResult> UpsertStrategy([FromBody] UpsertStrategyRequest req)
    {
        var result = await _service.UpsertStrategyAsync(req.DpdThreshold, req.ActionType, req.Description, req.NotificationTemplateId);
        return Ok(result);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates() => Ok(await _service.GetTemplatesAsync());

    [HttpPost("templates")]
    public async Task<IActionResult> UpsertTemplate([FromBody] UpsertTemplateRequest req)
    {
        var result = await _service.UpsertTemplateAsync(req.TemplateCode, req.Title, req.Body, req.Channel);
        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var count = await _service.ExecuteDunningAsync(today);
        return Ok(new { message = $"Dunning executed: {count} notifications sent." });
    }

    public class UpsertStrategyRequest
    {
        public int DpdThreshold { get; set; }
        public string ActionType { get; set; } = "";
        public string? Description { get; set; }
        public int? NotificationTemplateId { get; set; }
    }

    public class UpsertTemplateRequest
    {
        public string TemplateCode { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Channel { get; set; } = "InApp";
    }
}
