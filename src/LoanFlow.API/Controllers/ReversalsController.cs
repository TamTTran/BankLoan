using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ReversalsController : ControllerBase
{
    private readonly IReversalService _service;

    public ReversalsController(IReversalService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>POST /loans/{id}/request-reversal — Step 1: Request payment reversal</summary>
    [HttpPost("loans/{id:int}/request-reversal")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver}")]
    public async Task<IActionResult> RequestReversal(int id, [FromBody] RequestReversalDto request)
    {
        var result = await _service.RequestPaymentReversalAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /reversals/{id}/approve — Step 2: Approve pending reversal</summary>
    [HttpPost("reversals/{id:int}/approve")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> ApproveReversal(int id)
    {
        var result = await _service.ApproveReversalAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /reversals/{id}/reject — Step 2 (alt): Reject pending reversal</summary>
    [HttpPost("reversals/{id:int}/reject")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> RejectReversal(int id, [FromBody] ApproveReversalDto request)
    {
        var result = await _service.RejectReversalAsync(id, request.Notes ?? "", CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Reversal rejected." } });
    }

    /// <summary>GET /loans/{id}/reversals — List reversals for a loan</summary>
    [HttpGet("loans/{id:int}/reversals")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetReversalsForLoan(int id)
    {
        var data = await _service.GetReversalsForLoanAsync(id);
        return Ok(new { success = true, data });
    }

    /// <summary>GET /reversals/pending — All pending reversal requests</summary>
    [HttpGet("reversals/pending")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> GetPendingReversals()
    {
        var data = await _service.GetPendingReversalsAsync();
        return Ok(new { success = true, data });
    }

    /// <summary>POST /loans/{id}/adjust-fee — Direct fee adjustment</summary>
    [HttpPost("loans/{id:int}/adjust-fee")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Approver}")]
    public async Task<IActionResult> AdjustFee(int id, [FromBody] AdjustFeeDto request)
    {
        var result = await _service.AdjustFeeAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Fee adjusted." } });
    }
}
