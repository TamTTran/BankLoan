using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _service;
    public LoansController(ILoanService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Danh sách khoản vay</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] LoanFilterRequest filter)
        => Ok(await _service.GetListAsync(filter));

    /// <summary>Chi tiết khoản vay kèm lịch trả nợ</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Lịch trả nợ khoản vay</summary>
    [HttpGet("{id:int}/schedule")]
    public async Task<IActionResult> GetSchedule(int id)
        => Ok(await _service.GetScheduleAsync(id));

    /// <summary>Giải ngân khoản vay từ hồ sơ đã duyệt</summary>
    [HttpPost("disburse")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Disburse([FromBody] CreateLoanRequest request)
    {
        var result = await _service.DisburseAsync(request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Thu tiền trả nợ</summary>
    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Collector}")]
    public async Task<IActionResult> CollectPayment(int id, [FromBody] CollectPaymentRequest request)
    {
        var result = await _service.CollectPaymentAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Thanh toán đã được ghi nhận." });
    }
}
