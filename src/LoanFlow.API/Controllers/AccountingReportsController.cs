using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/accounting")]
[Authorize(Roles = "Admin,FinanceManager")]
public class AccountingReportsController : ControllerBase
{
    private readonly IFinancialStatementService _financialStatementService;

    public AccountingReportsController(IFinancialStatementService financialStatementService)
    {
        _financialStatementService = financialStatementService;
    }

    /// <summary>Generate Balance Sheet (Bảng cân đối kế toán)</summary>
    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet([FromQuery] DateOnly asOfDate)
    {
        var result = await _financialStatementService.GenerateBalanceSheetAsync(asOfDate);
        return Ok(result);
    }

    /// <summary>Generate Profit and Loss Statement (Báo cáo kết quả kinh doanh)</summary>
    [HttpGet("pnl")]
    public async Task<IActionResult> GetProfitAndLoss([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate)
    {
        var result = await _financialStatementService.GenerateProfitAndLossAsync(fromDate, toDate);
        return Ok(result);
    }

    /// <summary>Generate Trial Balance (Bảng cân đối số phát sinh)</summary>
    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] DateOnly asOfDate)
    {
        // Simple alias to balance sheet since Trial Balance has similar structure in our mockup
        var result = await _financialStatementService.GenerateBalanceSheetAsync(asOfDate);
        return Ok(result);
    }
}
