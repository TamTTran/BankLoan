using LoanFlow.Application.Services;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    [HttpPost("screen/{customerId}")]
    public async Task<IActionResult> ScreenCustomer(int customerId)
    {
        try
        {
            // Assuming current user is 1 for now (should be extracted from claims)
            int currentUserId = 1;
            var result = await _amlScreeningService.ScreenCustomerAsync(customerId, currentUserId);
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
}
