using LoanFlow.Application.DTOs.LoanApplication;
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
    private readonly ILoanApplicationService _service;
    public ApplicationsController(ILoanApplicationService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private int? CurrentBranchId => int.TryParse(User.FindFirstValue("branchId"), out var b) ? b : null;

    /// <summary>Danh sách hồ sơ vay</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ApplicationFilterRequest filter)
    {
        var result = await _service.GetListAsync(filter);
        return Ok(result);
    }

    /// <summary>Chi tiết hồ sơ vay</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Tạo hồ sơ vay mới (LoanOfficer)</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request)
    {
        var result = await _service.CreateAsync(request, CurrentUserId, CurrentBranchId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Nộp hồ sơ để duyệt</summary>
    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Submit(int id)
    {
        var result = await _service.SubmitAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Hồ sơ đã được nộp." });
    }

    /// <summary>Phê duyệt / Từ chối hồ sơ (Approver)</summary>
    [HttpPost("{id:int}/decide")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> Decide(int id, [FromBody] ApprovalDecisionRequest request)
    {
        var result = await _service.DecideAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Hủy hồ sơ</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _service.CancelAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Hồ sơ đã bị hủy." });
    }
}
