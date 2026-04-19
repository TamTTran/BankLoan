using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/accounting/periods")]
[Authorize(Roles = "Admin,Manager")]
public class AccountingPeriodController : ControllerBase
{
    private readonly IAccountingPeriodService _periodService;

    public AccountingPeriodController(IAccountingPeriodService periodService)
        => _periodService = periodService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _periodService.GetAllPeriodsAsync());

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var period = await _periodService.GetCurrentPeriodAsync();
        return period != null ? Ok(period) : NotFound("No open accounting period found.");
    }

    [HttpPost("{year}/{month}/close")]
    public async Task<IActionResult> Close(int year, int month, [FromBody] CloseRequest? req = null)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _periodService.ClosePeriodAsync(year, month, userId, req?.Notes);
        return result.IsSuccess ? Ok(new { message = $"Period {year}/{month:D2} closed successfully." }) : BadRequest(result.Error);
    }

    [HttpPost("{year}/{month}/reopen")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reopen(int year, int month)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _periodService.ReopenPeriodAsync(year, month, userId);
        return result.IsSuccess ? Ok(new { message = $"Period {year}/{month:D2} reopened." }) : BadRequest(result.Error);
    }

    public class CloseRequest
    {
        public string? Notes { get; set; }
    }
}
