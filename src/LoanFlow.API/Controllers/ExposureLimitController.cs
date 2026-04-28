using LoanFlow.Application.Services;
using LoanFlow.Application.DTOs.ApprovalFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/exposure")]
[Authorize]
public class ExposureLimitController : ControllerBase
{
    private readonly IExposureLimitService _service;
    private readonly IApprovalFlowService _approvalFlowService;
    public ExposureLimitController(IExposureLimitService service, IApprovalFlowService approvalFlowService)
    {
        _service = service;
        _approvalFlowService = approvalFlowService;
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> Get(int customerId)
    {
        var result = await _service.GetLimitAsync(customerId);
        return result != null ? Ok(result) : NotFound("No exposure limit set.");
    }

    [HttpPost("{customerId}")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> Set(int customerId, [FromBody] SetExposureLimitRequest req)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _service.SetLimitAsync(customerId, req.MaxExposure, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{customerId}/override")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Override(int customerId, [FromBody] OverrideRequest req)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "ExposureOverride",
            Payload = JsonSerializer.Serialize(new ExposureOverrideApprovalPayload
            {
                CustomerId = customerId,
                Reason = req.Reason
            })
        }, userId);
        return result.IsSuccess ? Accepted(new { success = true, approvalRequestId = result.Value.Id }) : BadRequest(result.Error);
    }

    [HttpPost("{customerId}/refresh")]
    public async Task<IActionResult> Refresh(int customerId)
    {
        await _service.RefreshExposureAsync(customerId);
        return Ok(new { message = "Exposure refreshed." });
    }

    public class OverrideRequest { public string Reason { get; set; } = ""; }
}
