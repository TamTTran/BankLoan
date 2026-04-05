using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    public DashboardController(IDashboardService service) => _service = service;

    /// <summary>Tổng quan danh mục khoản vay</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
        => Ok(await _service.GetSummaryAsync());

    /// <summary>Danh sách khoản vay quá hạn</summary>
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue()
        => Ok(await _service.GetOverdueListAsync());

    /// <summary>Cập nhật trạng thái quá hạn thủ công (admin)</summary>
    [HttpPost("update-overdue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateOverdue()
    {
        await _service.UpdateOverdueStatusAsync();
        return Ok(new { message = "Đã cập nhật trạng thái quá hạn." });
    }
}
