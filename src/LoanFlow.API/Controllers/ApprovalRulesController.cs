using LoanFlow.Application.DTOs;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/approval-rules")]
[Authorize(Roles = Roles.Admin)]
public class ApprovalRulesController : ControllerBase
{
    private readonly IApprovalRuleService _service;

    public ApprovalRulesController(IApprovalRuleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rules = await _service.GetAllAsync();
        return Ok(new { success = true, data = rules });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApprovalRuleRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value }); // Use Ok instead of CreatedAtAction since there's no GetById
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Rule deleted" } });
    }
}
