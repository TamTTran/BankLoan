using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/collaterals")]
[Authorize]
public class CollateralLifecycleController : ControllerBase
{
    private readonly ICollateralLifecycleService _service;
    public CollateralLifecycleController(ICollateralLifecycleService service) => _service = service;

    [HttpPost("{collateralId}/revalue")]
    public async Task<IActionResult> Revalue(int collateralId, [FromBody] RevalueRequest req)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.RevalueAsync(collateralId, req, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{collateralId}/revaluations")]
    public async Task<IActionResult> GetHistory(int collateralId)
        => Ok(await _service.GetRevaluationHistoryAsync(collateralId));

    [HttpPost("{collateralId}/release")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Release(int collateralId)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.ReleaseCollateralAsync(collateralId, userId);
        return result.IsSuccess ? Ok(new { message = "Collateral released." }) : BadRequest(result.Error);
    }

    [HttpPost("{collateralId}/seize")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Seize(int collateralId)
    {
        var userId = int.Parse(User.FindFirst("UserId")!.Value);
        var result = await _service.SeizeCollateralAsync(collateralId, userId);
        return result.IsSuccess ? Ok(new { message = "Collateral seized." }) : BadRequest(result.Error);
    }
}
