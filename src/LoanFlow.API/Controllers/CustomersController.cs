using LoanFlow.Application.DTOs.Customer;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    private readonly ICollateralService _collateralService;

    public CustomersController(ICustomerService service, ICollateralService collateralService)
    {
        _service = service;
        _collateralService = collateralService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>GET /customers — CUS-003</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetList([FromQuery] CustomerFilterRequest filter)
        => Ok(await _service.GetListAsync(filter));

    /// <summary>GET /customers/{id} — CUS-004</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { code = "APP-VAL-003", message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /customers — CUS-001</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var result = await _service.CreateAsync(request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, new { success = true, data = result.Value });
    }

    /// <summary>PATCH /customers/{id} — CUS-002</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /customers/{id}/check-credit — CUS-005</summary>
    [HttpPost("{id:int}/check-credit")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> CheckCreditStatus(int id)
    {
        var result = await _service.CheckCreditStatusAsync(id, CurrentUserId);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /customers/{id}/collaterals</summary>
    [HttpGet("{id:int}/collaterals")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver}")]
    public async Task<IActionResult> GetCollaterals(int id)
    {
        var collaterals = await _collateralService.GetByCustomerIdAsync(id);
        return Ok(new { success = true, data = collaterals });
    }
}
