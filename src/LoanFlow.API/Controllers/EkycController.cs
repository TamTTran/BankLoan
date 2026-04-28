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
    private readonly ILogger<EkycController> _logger;

    public EkycController(IEkycService ekycService, ILogger<EkycController> logger)
    {
        _ekycService = ekycService;
        _logger = logger;
    }

    /// <summary>Perform eKYC verification for a customer</summary>
    [HttpPost("verify/{customerId}")]
    public async Task<IActionResult> Verify(int customerId, [FromBody] VerifyRequest req)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        _logger.LogInformation(
            "eKYC verify requested: customerId={CustomerId} idType={IdType} userId={UserId}",
            customerId, req.IdType, userId);

        var result = await _ekycService.VerifyCustomerAsync(customerId, req.IdType, userId);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "eKYC verify failed: customerId={CustomerId} error={Error}",
                customerId, result.Error);

            // Return consistent JSON error shape — { message: "..." }
            return BadRequest(new { message = result.Error });
        }

        _logger.LogInformation(
            "eKYC verify completed: customerId={CustomerId} status={Status} provider={Provider}",
            customerId, result.Value!.Status, result.Value.Provider);

        return Ok(result.Value);
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
