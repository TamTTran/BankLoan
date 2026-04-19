using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/reports/regulatory")]
[Authorize(Roles = "Admin,Manager")]
public class RegulatoryReportsController : ControllerBase
{
    private readonly IRegulatoryReportingService _regService;

    public RegulatoryReportsController(IRegulatoryReportingService regService)
        => _regService = regService;

    /// <summary>NHNN Classification Report (Thông tư 02/2013)</summary>
    [HttpGet("nhnn-classification")]
    public async Task<IActionResult> NhnnClassification([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _regService.GenerateNhnnClassificationReportAsync(year, month);

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/csv")))
        {
            var csv = _regService.ToCsv(report.Rows);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"nhnn-classification-{year}{month:D2}.csv");
        }

        return Ok(report);
    }

    /// <summary>NHNN Provision Report</summary>
    [HttpGet("nhnn-provision")]
    public async Task<IActionResult> NhnnProvision([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _regService.GenerateNhnnProvisionReportAsync(year, month);

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/csv")))
        {
            var csv = _regService.ToCsv(report.GroupDetails);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"nhnn-provision-{year}{month:D2}.csv");
        }

        return Ok(report);
    }

    /// <summary>CIC Periodic Report</summary>
    [HttpGet("cic-periodic")]
    public async Task<IActionResult> CicPeriodic([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _regService.GenerateCicPeriodicReportAsync(year, month);

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/csv")))
        {
            var csv = _regService.ToCsv(report.Records);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"cic-periodic-{year}{month:D2}.csv");
        }

        return Ok(report);
    }
}
