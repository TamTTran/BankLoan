using System.Security.Claims;
using LoanFlow.Application.DTOs.ApprovalFlow;
using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/approval-requests")]
[Authorize]
public class ApprovalFlowController : ControllerBase
{
    private readonly IApprovalFlowService _service;

    public ApprovalFlowController(IApprovalFlowService service)
    {
        _service = service;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && int.TryParse(claim.Value, out var userId))
            return userId;

        // Fallback for tests or missing auth
        if (Request.Headers.TryGetValue("X-Test-User-Id", out var val) && int.TryParse(val, out var testId))
            return testId;

        throw new UnauthorizedAccessException("Cannot determine User ID.");
    }

    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitApprovalRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _service.SubmitRequestAsync(request, userId);
        
        if (!result.IsSuccess) return BadRequest(new { Error = result.Error });
        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var requests = await _service.GetPendingRequestsAsync();
        return Ok(new { Items = requests });
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> ApproveRequest(int id)
    {
        var checkerId = GetCurrentUserId();
        var result = await _service.ApproveRequestAsync(id, checkerId);

        if (!result.IsSuccess) return BadRequest(new { Error = result.Error });
        return Ok(result.Value);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> RejectRequest(int id, [FromBody] ProcessApprovalRequest request)
    {
        var checkerId = GetCurrentUserId();
        var result = await _service.RejectRequestAsync(id, request, checkerId);

        if (!result.IsSuccess) return BadRequest(new { Error = result.Error });
        return Ok(result.Value);
    }
}
