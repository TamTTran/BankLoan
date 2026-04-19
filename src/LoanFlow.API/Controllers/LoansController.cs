using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.DTOs.ApprovalFlow;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoanFlow.API.Filters;
using System.Security.Claims;
using System.Text.Json;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _service;
    private readonly ICollateralService _collateralService;
    private readonly IApprovalFlowService _approvalFlowService;

    public LoansController(ILoanService service, ICollateralService collateralService, IApprovalFlowService approvalFlowService)
    {
        _service = service;
        _collateralService = collateralService;
        _approvalFlowService = approvalFlowService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Loan list & detail ───────────────────────────────────────────────────

    /// <summary>GET /loans — LOAN-003</summary>
    [HttpGet("loans")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetList([FromQuery] LoanFilterRequest filter)
        => Ok(new { success = true, data = await _service.GetListAsync(filter) });

    /// <summary>GET /loans/{id} — LOAN-003</summary>
    [HttpGet("loans/{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    // ── Create loan from approved application ────────────────────────────────

    /// <summary>POST /applications/{id}/create-loan — LOAN-001</summary>
    [HttpPost("applications/{applicationId:int}/create-loan")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> CreateLoan(int applicationId)
    {
        var result = await _service.CreateLoanAsync(applicationId, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, new { success = true, data = result.Value });
    }

    // ── Disbursement ─────────────────────────────────────────────────────────

    /// <summary>POST /loans/{id}/disburse — LOAN-002</summary>
    [HttpPost("loans/{id:int}/disburse")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    [IdempotencyFilter]
    public async Task<IActionResult> Disburse(int id, [FromBody] DisburseRequest request)
    {
        return await SubmitApprovalAsync(
            "LoanDisburse",
            new DisburseApprovalPayload
            {
                LoanId = id,
                Request = request
            });
    }

    // ── Schedule ─────────────────────────────────────────────────────────────

    /// <summary>GET /loans/{id}/schedule — SCH-002</summary>
    [HttpGet("loans/{id:int}/schedule")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetSchedule(int id)
        => Ok(new { success = true, data = await _service.GetScheduleAsync(id) });

    // ── Payments ─────────────────────────────────────────────────────────────

    /// <summary>POST /loans/{id}/payments — PAY-001</summary>
    [HttpPost("loans/{id:int}/payments")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    [IdempotencyFilter]
    public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordPaymentRequest request)
    {
        var result = await _service.RecordPaymentAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Payment recorded." } });
    }

    /// <summary>GET /loans/{id}/payments — PAY-002</summary>
    [HttpGet("loans/{id:int}/payments")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetPayments(int id)
        => Ok(new { success = true, data = await _service.GetPaymentsAsync(id) });

    // ── Collection notes ─────────────────────────────────────────────────────

    /// <summary>POST /loans/{id}/collection-notes — OD-003</summary>
    [HttpPost("loans/{id:int}/collection-notes")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Collector}")]
    public async Task<IActionResult> AddCollectionNote(int id, [FromBody] AddCollectionNoteRequest request)
    {
        var result = await _service.AddCollectionNoteAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Collection note added." } });
    }

    // ── Phase 2: Early Settlement & Late Fees ────────────────────────────────

    /// <summary>GET /loans/{id}/early-settle-preview</summary>
    [HttpGet("loans/{id:int}/early-settle-preview")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> PreviewEarlySettle(int id, [FromQuery] DateOnly settlementDate)
    {
        var result = await _service.PreviewEarlySettleAsync(id, settlementDate);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /loans/{id}/early-settle</summary>
    [HttpPost("loans/{id:int}/early-settle")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    [IdempotencyFilter]
    public async Task<IActionResult> EarlySettle(int id, [FromBody] EarlySettleRequest request)
    {
        return await SubmitApprovalAsync(
            "LoanEarlySettle",
            new EarlySettleApprovalPayload
            {
                LoanId = id,
                Request = request
            });
    }

    /// <summary>POST /late-fees/{id}/waive</summary>
    [HttpPost("late-fees/{id:int}/waive")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> WaiveLateFee(int id)
    {
        var approval = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "LateFeeWaive",
            Payload = JsonSerializer.Serialize(new LateFeeWaiveApprovalPayload
            {
                LateFeeId = id
            })
        }, CurrentUserId);

        if (!approval.IsSuccess) return BadRequest(new { success = false, error = new { message = approval.Error } });
        return Accepted(new { success = true, approvalRequestId = approval.Value.Id });
    }

    // ── Phase 3: Loan Collaterals ───────────────────────────────────────────

    /// <summary>GET /loans/{id}/collaterals</summary>
    [HttpGet("loans/{id:int}/collaterals")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver}")]
    public async Task<IActionResult> GetLoanCollaterals(int id)
    {
        var collaterals = await _collateralService.GetByLoanIdAsync(id);
        return Ok(new { success = true, data = collaterals });
    }

    /// <summary>POST /loans/{id}/collaterals</summary>
    [HttpPost("loans/{id:int}/collaterals")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> AllocateCollateral(int id, [FromBody] LoanFlow.Application.DTOs.Collateral.AllocateCollateralRequest request)
    {
        var result = await _collateralService.AllocateToLoanAsync(id, request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Collateral allocated to loan." } });
    }

    /// <summary>DELETE /loans/{id}/collaterals/{collateralId}</summary>
    [HttpDelete("loans/{id:int}/collaterals/{collateralId:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> RemoveCollateral(int id, int collateralId)
    {
        var result = await _collateralService.RemoveFromLoanAsync(id, collateralId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Collateral removed from loan." } });
    }

    // ── Overdue list ─────────────────────────────────────────────────────────

    /// <summary>GET /overdue — OD-002</summary>
    [HttpGet("overdue")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetOverdueList()
        => Ok(new { success = true, data = await _service.GetOverdueListAsync() });

    // ── Wave 4: Overpayment Suspense ─────────────────────────────────────────

    /// <summary>POST /loans/suspense/{id}/apply</summary>
    [HttpPost("suspense/{id:int}/apply")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    [IdempotencyFilter]
    public async Task<IActionResult> ApplySuspense(int id)
    {
        return await SubmitApprovalAsync(
            "SuspenseApply",
            new ApplySuspenseApprovalPayload
            {
                SuspenseId = id
            });
    }

    private async Task<IActionResult> SubmitApprovalAsync(string actionType, object payload)
    {
        var approval = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = actionType,
            Payload = JsonSerializer.Serialize(payload)
        }, CurrentUserId);

        if (!approval.IsSuccess) return BadRequest(new { success = false, error = new { message = approval.Error } });
        return Accepted(new { success = true, approvalRequestId = approval.Value.Id });
    }
}
