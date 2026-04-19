using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService) => _branchService = branchService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _branchService.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest req)
    {
        var result = await _branchService.CreateAsync(req);
        return result.IsSuccess ? Created($"/api/branches/{result.Value!.Id}", result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBranchRequest req)
    {
        var result = await _branchService.UpdateAsync(id, req);
        return result.IsSuccess ? Ok(new { message = "Branch updated." }) : BadRequest(result.Error);
    }

    [HttpGet("{id}/portfolio")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetPortfolio(int id)
    {
        var portfolio = await _branchService.GetBranchPortfolioAsync(id);
        return portfolio != null ? Ok(portfolio) : NotFound("Branch not found.");
    }
}
