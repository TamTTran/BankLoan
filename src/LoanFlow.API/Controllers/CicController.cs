using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/cic")]
[Authorize]
public class CicController : ControllerBase
{
    private readonly ICicService _cicService;

    public CicController(ICicService cicService) => _cicService = cicService;

    /// <summary>Perform CIC inquiry for a customer</summary>
    [HttpPost("inquire/{customerId}")]
    public async Task<IActionResult> Inquire(int customerId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _cicService.InquireCicAsync(customerId, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Get CIC inquiry history for a customer</summary>
    [HttpGet("history/{customerId}")]
    public async Task<IActionResult> GetHistory(int customerId)
    {
        var history = await _cicService.GetHistoryAsync(customerId);
        return Ok(history);
    }
}
