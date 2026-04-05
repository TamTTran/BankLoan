using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _service;
    public LoansController(ILoanService service) => _service = service;

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
    public async Task<IActionResult> Disburse(int id, [FromBody] DisburseRequest request)
    {
        var result = await _service.DisburseAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
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

    // ── Overdue list ─────────────────────────────────────────────────────────

    /// <summary>GET /overdue — OD-002</summary>
    [HttpGet("overdue")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetOverdueList()
        => Ok(new { success = true, data = await _service.GetOverdueListAsync() });
}
