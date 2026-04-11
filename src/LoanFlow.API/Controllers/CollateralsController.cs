using LoanFlow.Application.DTOs.Collateral;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/collaterals")]
[Authorize]
public class CollateralsController : ControllerBase
{
    private readonly ICollateralService _service;
    public CollateralsController(ICollateralService service) => _service = service;

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Create([FromBody] CreateCollateralRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, new { success = true, data = result.Value });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCollateralRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }
}
