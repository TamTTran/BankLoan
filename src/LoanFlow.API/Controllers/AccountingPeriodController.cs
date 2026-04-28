using LoanFlow.Application.Services;
using LoanFlow.Application.DTOs.ApprovalFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/accounting/periods")]
[Authorize(Roles = "Admin,Manager")]
public class AccountingPeriodController : ControllerBase
{
    private readonly IAccountingPeriodService _periodService;
    private readonly IApprovalFlowService _approvalFlowService;

    public AccountingPeriodController(IAccountingPeriodService periodService, IApprovalFlowService approvalFlowService)
    {
        _periodService = periodService;
        _approvalFlowService = approvalFlowService;
    }

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
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "AccountingPeriodClose",
            Payload = JsonSerializer.Serialize(new AccountingPeriodCloseApprovalPayload
            {
                Year = year,
                Month = month,
                Notes = req?.Notes
            })
        }, userId);
        return result.IsSuccess ? Accepted(new { success = true, approvalRequestId = result.Value.Id }) : BadRequest(result.Error);
    }

    [HttpPost("{year}/{month}/reopen")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reopen(int year, int month)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _approvalFlowService.SubmitRequestAsync(new SubmitApprovalRequest
        {
            ActionType = "AccountingPeriodReopen",
            Payload = JsonSerializer.Serialize(new AccountingPeriodReopenApprovalPayload
            {
                Year = year,
                Month = month
            })
        }, userId);
        return result.IsSuccess ? Accepted(new { success = true, approvalRequestId = result.Value.Id }) : BadRequest(result.Error);
    }

    public class CloseRequest
    {
        public string? Notes { get; set; }
    }
}
