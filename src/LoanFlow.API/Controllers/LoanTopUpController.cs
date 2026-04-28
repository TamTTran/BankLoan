using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/topups")]
[Authorize]
public class LoanTopUpController : ControllerBase
{
    private readonly ILoanTopUpService _service;
    public LoanTopUpController(ILoanTopUpService service) => _service = service;

    [HttpPost("{loanId}")]
    public async Task<IActionResult> RequestTopUp(int loanId, [FromBody] TopUpRequest req)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _service.RequestTopUpAsync(loanId, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{topUpId}/approve")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> Approve(int topUpId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _service.ApproveTopUpAsync(topUpId, userId);
        return result.IsSuccess ? Ok(new { message = "Top-up approved and disbursed." }) : BadRequest(result.Error);
    }

    [HttpPost("{topUpId}/reject")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> Reject(int topUpId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _service.RejectTopUpAsync(topUpId, userId);
        return result.IsSuccess ? Ok(new { message = "Top-up rejected." }) : BadRequest(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? loanId)
        => Ok(await _service.GetTopUpsAsync(loanId));
}
