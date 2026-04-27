using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/risk")]
[Authorize(Roles = "Admin,Manager,RiskOfficer")]
public class RiskManagementController : ControllerBase
{
    private readonly IRiskModelService _riskModelService;
    private readonly IStressTestService _stressTestService;
    private readonly IConcentrationLimitService _concentrationLimitService;

    public RiskManagementController(
        IRiskModelService riskModelService,
        IStressTestService stressTestService,
        IConcentrationLimitService concentrationLimitService)
    {
        _riskModelService = riskModelService;
        _stressTestService = stressTestService;
        _concentrationLimitService = concentrationLimitService;
    }

    /// <summary>Expected Loss (EL) Summary for the entire portfolio</summary>
    [HttpGet("el-summary")]
    public async Task<IActionResult> GetElSummary()
    {
        var result = await _riskModelService.GetPortfolioExpectedLossAsync();
        return Ok(result);
    }

    /// <summary>Expected Loss (EL) Detail for a specific loan</summary>
    [HttpGet("el-loan/{loanId}")]
    public async Task<IActionResult> GetElByLoan(int loanId)
    {
        try
        {
            var result = await _riskModelService.CalculateExpectedLossAsync(loanId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Run Stress Test Scenarios</summary>
    [HttpPost("stress-test")]
    public async Task<IActionResult> RunStressTest()
    {
        var result = await _stressTestService.RunStressTestsAsync();
        return Ok(result);
    }

    /// <summary>Concentration Limit Report</summary>
    [HttpGet("concentration")]
    public async Task<IActionResult> GetConcentrationLimits()
    {
        var result = await _concentrationLimitService.GetConcentrationReportAsync();
        return Ok(result);
    }
}
