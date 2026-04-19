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
    private readonly ILoanAdjustmentService _loanAdjustmentService;

    public LoanAdjustmentsController(IApprovalFlowService approvalFlowService, ILoanAdjustmentService loanAdjustmentService)
    {
        _approvalFlowService = approvalFlowService;
        _loanAdjustmentService = loanAdjustmentService;
    }

    [HttpPost("manual-adjustment")]
    public async Task<IActionResult> RequestManualAdjustment([FromBody] LoanAdjustmentPayloadDto payload)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        
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
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        
        var request = new LoanFlow.Application.DTOs.ApprovalFlow.SubmitApprovalRequest
        {
            ActionType = "DisbursementCorrect",
            Payload = System.Text.Json.JsonSerializer.Serialize(payload)
        };

        var approvalResult = await _approvalFlowService.SubmitRequestAsync(request, userId);

        return Ok(new { success = true, approvalRequestId = approvalResult.Value.Id });
    }

    [HttpPost("{approvalId}/apply")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> ApplyAdjustment(int approvalId)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        
        var result = await _loanAdjustmentService.ApplyApprovedAdjustmentAsync(approvalId, userId);
        return result.IsSuccess ? Ok(new { message = "Adjustment applied successfully." }) : BadRequest(result.Error);
    }
}
