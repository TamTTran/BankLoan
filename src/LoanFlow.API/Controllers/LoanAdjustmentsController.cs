using LoanFlow.Application.DTOs.LoanAdjustment;
using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/adjustments")]
[Authorize]
public class LoanAdjustmentsController : ControllerBase
{
    private readonly IApprovalFlowService _approvalFlowService;

    public LoanAdjustmentsController(IApprovalFlowService approvalFlowService)
    {
        _approvalFlowService = approvalFlowService;
    }

    [HttpPost("manual-adjustment")]
    public async Task<IActionResult> RequestManualAdjustment([FromBody] LoanAdjustmentPayloadDto payload)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        
        var request = new LoanFlow.Application.DTOs.ApprovalFlow.SubmitApprovalRequest
        {
            ActionType = "ManualAdjustment",
            Payload = System.Text.Json.JsonSerializer.Serialize(payload)
        };

        var approvalResult = await _approvalFlowService.SubmitRequestAsync(request, userId);

        return Ok(new { success = true, approvalRequestId = approvalResult.Value.Id });
    }

    [HttpPost("disbursement-correction")]
    public async Task<IActionResult> RequestDisbursementCorrection([FromBody] object payload)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        
        var request = new LoanFlow.Application.DTOs.ApprovalFlow.SubmitApprovalRequest
        {
            ActionType = "DisbursementCorrect",
            Payload = System.Text.Json.JsonSerializer.Serialize(payload)
        };

        var approvalResult = await _approvalFlowService.SubmitRequestAsync(request, userId);

        return Ok(new { success = true, approvalRequestId = approvalResult.Value.Id });
    }
}
