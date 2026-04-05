using LoanFlow.Application.DTOs.LoanApplication;
using LoanFlow.Application.DTOs.LoanProduct;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly ILoanApplicationService _appService;
    public ApplicationsController(ILoanApplicationService appService)
    {
        _appService = appService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>GET /applications — APP-004</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetList([FromQuery] ApplicationFilterRequest filter)
        => Ok(new { success = true, data = await _appService.GetListAsync(filter) });

    /// <summary>GET /applications/{id} — APP-004</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _appService.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /applications — APP-001</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request)
    {
        var result = await _appService.CreateAsync(request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, new { success = true, data = result.Value });
    }

    /// <summary>PATCH /applications/{id} — APP-002</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateApplicationRequest request)
    {
        var result = await _appService.UpdateAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /applications/{id}/submit — APP-003</summary>
    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Submit(int id)
    {
        var result = await _appService.SubmitAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Application submitted." } });
    }

    /// <summary>POST /applications/preview — APP-005</summary>
    [HttpPost("preview")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Preview([FromBody] InstallmentPreviewRequest request)
    {
        var result = await _appService.PreviewAsync(request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /approvals/queue — APR-001</summary>
    [HttpGet("/api/approvals/queue")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> GetApprovalQueue()
    {
        var filter = new ApplicationFilterRequest { Status = "Submitted", PageSize = 100 };
        return Ok(new { success = true, data = await _appService.GetListAsync(filter) });
    }

    /// <summary>POST /applications/{id}/approve — APR-002</summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _appService.ApproveAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Application approved." } });
    }

    /// <summary>POST /applications/{id}/reject — APR-003</summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectApplicationRequest request)
    {
        var result = await _appService.RejectAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Application rejected." } });
    }
}
