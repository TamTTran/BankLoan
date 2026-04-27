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
    private readonly ICapitalAdequacyService _carService;
    private readonly ILiquidityReportService _liquidityService;
    private readonly IInterestRateReportService _interestRateService;

    public RegulatoryReportsController(
        IRegulatoryReportingService regService,
        ICapitalAdequacyService carService,
        ILiquidityReportService liquidityService,
        IInterestRateReportService interestRateService)
    {
        _regService = regService;
        _carService = carService;
        _liquidityService = liquidityService;
        _interestRateService = interestRateService;
    }

    /// <summary>NHNN Classification Report (Thông tư 02/2013)</summary>
    [HttpGet("nhnn-classification")]
    public async Task<IActionResult> NhnnClassification([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _regService.GenerateNhnnClassificationReportAsync(year, month);

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")))
        {
            var excel = _regService.ToExcel(report.Details, "NhnnClassification");
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"nhnn-classification-{year}{month:D2}.xlsx");
        }

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("application/xml")))
        {
            var xml = _regService.ToXml(report.Details, "NhnnClassificationReport");
            return Content(xml, "application/xml");
        }

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

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")))
        {
            var excel = _regService.ToExcel(report.GroupDetails, "NhnnProvision");
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"nhnn-provision-{year}{month:D2}.xlsx");
        }

        if (Request.Headers.Accept.Any(a => a != null && a.Contains("application/xml")))
        {
            var xml = _regService.ToXml(report.GroupDetails, "NhnnProvisionReport");
            return Content(xml, "application/xml");
        }

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

    [HttpGet("car")]
    public async Task<IActionResult> CarReport([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _carService.CalculateCarAsync(year, month);
        return Ok(report);
    }

    /// <summary>Liquidity Report (LCR & NSFR)</summary>
    [HttpGet("liquidity")]
    public async Task<IActionResult> LiquidityReport([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _liquidityService.GenerateReportAsync(year, month);
        return Ok(report);
    }

    /// <summary>Interest Rate Report</summary>
    [HttpGet("interest-rate")]
    public async Task<IActionResult> InterestRateReport([FromQuery] int year, [FromQuery] int month)
    {
        var report = await _interestRateService.GenerateReportAsync(year, month);
        return Ok(report);
    }
}
