using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/ekyc")]
[Authorize]
public class EkycController : ControllerBase
{
    private readonly IEkycService _ekycService;

    public EkycController(IEkycService ekycService) => _ekycService = ekycService;

    /// <summary>Perform eKYC verification for a customer</summary>
    [HttpPost("verify/{customerId}")]
    public async Task<IActionResult> Verify(int customerId, [FromBody] VerifyRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _ekycService.VerifyCustomerAsync(customerId, req.IdType, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Get eKYC verification history for a customer</summary>
    [HttpGet("history/{customerId}")]
    public async Task<IActionResult> GetHistory(int customerId)
    {
        var history = await _ekycService.GetHistoryAsync(customerId);
        return Ok(history);
    }

    public class VerifyRequest
    {
        public string IdType { get; set; } = "CCCD";
    }
}
