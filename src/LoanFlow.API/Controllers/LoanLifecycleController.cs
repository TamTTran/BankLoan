using System.Security.Claims;
using System.Text.Json;
using LoanFlow.Application.DTOs.ApprovalFlow;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/loans/{loanId:int}")]
[Authorize]
public class LoanLifecycleController : ControllerBase
{
    private readonly ILoanLifecycleService _service;
    private readonly IApprovalFlowService _approvalFlowService;

    public LoanLifecycleController(ILoanLifecycleService service, IApprovalFlowService approvalFlowService)
    {
        _service = service;
        _approvalFlowService = approvalFlowService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── BUP-201: Restructure ─────────────────────────────────────────────────

    /// <summary>POST /loans/{id}/restructure — Cơ cấu lại khoản vay (tenor / rate mới)</summary>
    [HttpPost("restructure")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> Restructure(int loanId, [FromBody] RestructureRequest request)
    {
        var result = await _service.RestructureAsync(loanId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("restructure/request")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> RequestRestructure(int loanId, [FromBody] RestructureRequest request)
    {
        var approval = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "LoanRestructure",
            Payload = JsonSerializer.Serialize(new RestructureApprovalPayload
            {
                LoanId = loanId,
                Request = request
            })
        }, CurrentUserId);

        if (!approval.IsSuccess) return BadRequest(new { success = false, error = approval.Error });
        return Accepted(new { success = true, approvalRequestId = approval.Value.Id });
    }

    // ── BUP-202: Reschedule ──────────────────────────────────────────────────

    /// <summary>POST /loans/{id}/reschedule — Đổi ngày trả nợ hàng tháng</summary>
    [HttpPost("reschedule")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> Reschedule(int loanId, [FromBody] RescheduleRequest request)
    {
        var result = await _service.RescheduleAsync(loanId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("reschedule/request")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> RequestReschedule(int loanId, [FromBody] RescheduleRequest request)
    {
        var approval = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "LoanReschedule",
            Payload = JsonSerializer.Serialize(new RescheduleApprovalPayload
            {
                LoanId = loanId,
                Request = request
            })
        }, CurrentUserId);

        if (!approval.IsSuccess) return BadRequest(new { success = false, error = approval.Error });
        return Accepted(new { success = true, approvalRequestId = approval.Value.Id });
    }

    // ── BUP-203: Write-Off ───────────────────────────────────────────────────

    /// <summary>POST /loans/{id}/write-off — Xóa nợ (chỉ Admin hoặc Manager)</summary>
    [HttpPost("write-off")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> WriteOff(int loanId, [FromBody] WriteOffRequest request)
    {
        var result = await _service.WriteOffAsync(loanId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("write-off/request")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> RequestWriteOff(int loanId, [FromBody] WriteOffRequest request)
    {
        var approval = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "LoanWriteOff",
            Payload = JsonSerializer.Serialize(new WriteOffApprovalPayload
            {
                LoanId = loanId,
                Request = request
            })
        }, CurrentUserId);

        if (!approval.IsSuccess) return BadRequest(new { success = false, error = approval.Error });
        return Accepted(new { success = true, approvalRequestId = approval.Value.Id });
    }

    /// <summary>POST /loans/{id}/recovery — Ghi nhận thu hồi tiền sau write-off</summary>
    [HttpPost("recovery")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Manager}")]
    public async Task<IActionResult> RecordRecovery(int loanId, [FromBody] RecordRecoveryRequest request)
    {
        var result = await _service.RecordRecoveryAsync(loanId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

// ── BUP-204: Customer Exposure Limit ────────────────────────────────────────

[ApiController]
[Route("api/customers/{customerId:int}/exposure")]
[Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
public class CustomerExposureController : ControllerBase
{
    private readonly ILoanLifecycleService _service;

    public CustomerExposureController(ILoanLifecycleService service) => _service = service;

    /// <summary>GET /customers/{id}/exposure — Tổng dư nợ và giới hạn của khách hàng</summary>
    [HttpGet]
    public async Task<IActionResult> GetExposure(int customerId)
    {
        var result = await _service.GetCustomerExposureAsync(customerId);
        if (!result.IsSuccess) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
