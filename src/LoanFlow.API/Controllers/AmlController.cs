using LoanFlow.Application.Services;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/aml")]
[Authorize(Roles = "Admin,Manager,ComplianceOfficer")]
public class AmlController : ControllerBase
{
    private readonly IAmlScreeningService _amlScreeningService;
    private readonly LoanFlowDbContext _db;

    public AmlController(IAmlScreeningService amlScreeningService, LoanFlowDbContext db)
    {
        _amlScreeningService = amlScreeningService;
        _db = db;
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 1;

    [HttpPost("screen/{customerId}")]
    public async Task<IActionResult> ScreenCustomer(int customerId)
    {
        try
        {
            var result = await _amlScreeningService.ScreenCustomerAsync(customerId, CurrentUserId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("assessments/{customerId}")]
    public async Task<IActionResult> GetAssessments(int customerId)
    {
        var history = await _db.AmlRiskAssessments
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.LastScreeningDate)
            .ToListAsync();
        return Ok(history);
    }

    /// <summary>AML Alerts — pending/draft STRs that need attention</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.SuspiciousTransactionReports
            .Include(r => r.Customer)
            .Where(r => r.Status == "Draft" || r.Status == "PendingApproval")
            .OrderByDescending(r => r.ReportDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.StrNumber,
                r.CustomerId,
                CustomerName = r.Customer.FullName,
                r.ReportDate,
                r.SuspicionType,
                r.Description,
                r.Status
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("str")]
    public async Task<IActionResult> GetSuspiciousTransactionReports([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.SuspiciousTransactionReports
            .Include(r => r.Customer)
            .OrderByDescending(r => r.ReportDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new {
                r.Id,
                r.StrNumber,
                r.CustomerId,
                CustomerName = r.Customer.FullName,
                r.ReportDate,
                r.SuspicionType,
                r.Status
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost("str")]
    public async Task<IActionResult> CreateSuspiciousTransactionReport([FromBody] CreateStrRequest request)
    {
        if (request.CustomerId <= 0)
            return BadRequest(new { error = "CustomerId is required." });

        if (string.IsNullOrWhiteSpace(request.NarrativeDescription))
            return BadRequest(new { error = "NarrativeDescription is required." });

        if (!await _db.Customers.AnyAsync(c => c.Id == request.CustomerId))
            return NotFound(new { error = "Customer not found." });

        var suspicionType = request.ReportType?.Trim().ToUpperInvariant() switch
        {
            "CTR" => "LargeCash",
            "STR" or "" or null => "UnusualPattern",
            "STRUCTURING" => "Structuring",
            "LARGECASH" => "LargeCash",
            "RAPIDMOVEMENT" => "RapidMovement",
            "UNUSUALPATTERN" => "UnusualPattern",
            _ => "UnusualPattern"
        };

        var relatedIds = request.SuspectedTransactionId.HasValue
            ? JsonSerializer.Serialize(new[] { request.SuspectedTransactionId.Value })
            : "[]";

        var str = new SuspiciousTransactionReport
        {
            CustomerId = request.CustomerId,
            StrNumber = $"STR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            ReportDate = DateTime.UtcNow,
            SuspicionType = suspicionType,
            Description = request.NarrativeDescription.Trim(),
            RelatedTransactionIdsJson = relatedIds,
            Status = "PendingApproval",
            CreatedByUserId = CurrentUserId
        };

        _db.SuspiciousTransactionReports.Add(str);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = str });
    }

    public sealed class CreateStrRequest
    {
        public int CustomerId { get; set; }
        public int? SuspectedTransactionId { get; set; }
        public string ReportType { get; set; } = "STR";
        public string NarrativeDescription { get; set; } = "";
    }
}
