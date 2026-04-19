using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LoanFlow.Application.DTOs.ApprovalFlow;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/advanced-loans")]
[Authorize(Roles = "Admin,Manager")]
public class AdvancedLendingController : ControllerBase
{
    private readonly ILoanLifecycleService _service;
    private readonly IApprovalFlowService _approvalFlowService;

    public AdvancedLendingController(ILoanLifecycleService service, IApprovalFlowService approvalFlowService)
    {
        _service = service;
        _approvalFlowService = approvalFlowService;
    }

    [HttpPost("{id}/restructure")]
    public async Task<IActionResult> Restructure(int id, [FromBody] RestructureRequest req)
    {
        return await SubmitApprovalAsync(
            "LoanRestructure",
            new RestructureApprovalPayload
            {
                LoanId = id,
                Request = req
            });
    }

    [HttpPost("{id}/reschedule")]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleRequest req)
    {
        return await SubmitApprovalAsync(
            "LoanReschedule",
            new RescheduleApprovalPayload
            {
                LoanId = id,
                Request = req
            });
    }

    [HttpPost("{id}/write-off")]
    public async Task<IActionResult> WriteOff(int id, [FromBody] WriteOffRequest req)
    {
        return await SubmitApprovalAsync(
            "LoanWriteOff",
            new WriteOffApprovalPayload
            {
                LoanId = id,
                Request = req
            });
    }

    [HttpPost("{id}/recovery")]
    public async Task<IActionResult> Recovery(int id, [FromBody] RecordRecoveryRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.RecordRecoveryAsync(id, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    private async Task<IActionResult> SubmitApprovalAsync(string actionType, object payload)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = actionType,
            Payload = JsonSerializer.Serialize(payload)
        }, userId);

        return result.IsSuccess
            ? Accepted(new { success = true, approvalRequestId = result.Value.Id })
            : BadRequest(result.Error);
    }
}
