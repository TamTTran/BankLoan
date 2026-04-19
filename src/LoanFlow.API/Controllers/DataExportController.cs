using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize(Roles = "Admin,Manager")]
public class DataExportController : ControllerBase
{
    private readonly IDataExportService _service;
    public DataExportController(IDataExportService service) => _service = service;

    [HttpGet("loans")]
    public async Task<IActionResult> ExportLoans()
    {
        var data = await _service.ExportLoansAsync();
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/csv")))
            return File(Encoding.UTF8.GetBytes(data.CsvContent), "text/csv", "loans_export.csv");
        return Ok(data);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> ExportCustomers()
    {
        var data = await _service.ExportCustomersAsync();
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/csv")))
            return File(Encoding.UTF8.GetBytes(data.CsvContent), "text/csv", "customers_export.csv");
        return Ok(data);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> ExportTransactions([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var data = await _service.ExportTransactionsAsync(from, to);
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/csv")))
            return File(Encoding.UTF8.GetBytes(data.CsvContent), "text/csv", $"transactions_{from}_{to}.csv");
        return Ok(data);
    }

    [HttpGet("portfolio-health")]
    public async Task<IActionResult> GetPortfolioHealth()
        => Ok(await _service.GetPortfolioHealthAsync());
}
