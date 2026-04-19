using LoanFlow.Application.Services;
using LoanFlow.Application.DTOs.ApprovalFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/collaterals")]
[Authorize]
public class CollateralLifecycleController : ControllerBase
{
    private readonly ICollateralLifecycleService _service;
    private readonly IApprovalFlowService _approvalFlowService;
    public CollateralLifecycleController(ICollateralLifecycleService service, IApprovalFlowService approvalFlowService)
    {
        _service = service;
        _approvalFlowService = approvalFlowService;
    }

    [HttpPost("{collateralId}/revalue")]
    public async Task<IActionResult> Revalue(int collateralId, [FromBody] RevalueRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.RevalueAsync(collateralId, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{collateralId}/revaluations")]
    public async Task<IActionResult> GetHistory(int collateralId)
        => Ok(await _service.GetRevaluationHistoryAsync(collateralId));

    [HttpPost("{collateralId}/release")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Release(int collateralId)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "CollateralRelease",
            Payload = JsonSerializer.Serialize(new CollateralReleaseApprovalPayload
            {
                CollateralId = collateralId
            })
        }, userId);
        return result.IsSuccess ? Accepted(new { success = true, approvalRequestId = result.Value.Id }) : BadRequest(result.Error);
    }

    [HttpPost("{collateralId}/seize")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Seize(int collateralId)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "CollateralSeize",
            Payload = JsonSerializer.Serialize(new CollateralSeizeApprovalPayload
            {
                CollateralId = collateralId
            })
        }, userId);
        return result.IsSuccess ? Accepted(new { success = true, approvalRequestId = result.Value.Id }) : BadRequest(result.Error);
    }
}
