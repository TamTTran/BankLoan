using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterbankController : ControllerBase
{
    private readonly ICoreBankingIntegrationService _coreBankingService;
    private readonly IVirtualAccountMatchingService _vaMatchingService;
    private readonly LoanFlowDbContext _db;

    public InterbankController(
        ICoreBankingIntegrationService coreBankingService,
        IVirtualAccountMatchingService vaMatchingService,
        LoanFlowDbContext db)
    {
        _coreBankingService = coreBankingService;
        _vaMatchingService = vaMatchingService;
        _db = db;
    }

    [HttpGet("journals")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetJournals([FromQuery] string? status, [FromQuery] string? businessDate)
    {
        var query = _db.JournalEntries.AsQueryable();

        if (!string.IsNullOrEmpty(status) && status != "All")
            query = query.Where(j => j.SyncStatus == status);

        if (!string.IsNullOrEmpty(businessDate) && DateOnly.TryParse(businessDate, out var date))
            query = query.Where(j => j.BusinessDate == date);

        var entries = await query
            .OrderByDescending(j => j.PostedAt)
            .Take(100) // Simplification: just return top 100 for monitoring
            .Select(j => new
            {
                j.Id,
                j.ReferenceNumber,
                j.BusinessDate,
                j.Description,
                j.SyncStatus,
                j.SyncedAt,
                j.PostedAt
            })
            .ToListAsync();

        return Ok(entries);
    }

    [HttpPost("sync-journals")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SyncDailyBatch([FromBody] SyncDailyBatchRequest request)
    {
        var result = await _coreBankingService.PushDailyBatchAsync(request.BusinessDate);
        if (result.IsSuccess) return Ok(new { message = "Batch sync completed successfully." });
        return BadRequest(new { message = result.Error });
    }

    [HttpPost("sync-journals/{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SyncSingleJournal(int id)
    {
        var result = await _coreBankingService.PushJournalEntryAsync(id);
        if (result.IsSuccess) return Ok(new { message = "Journal synced successfully." });
        return BadRequest(new { message = result.Error });
    }

    // This webhook would ideally have API Key auth or IP whitelisting in production
    [HttpPost("virtual-account-webhook")]
    [AllowAnonymous] 
    public async Task<IActionResult> VirtualAccountWebhook([FromBody] VirtualAccountWebhookPayload payload)
    {
        var result = await _vaMatchingService.ProcessIncomingPaymentAsync(
            payload.VirtualAccountNo,
            payload.Amount,
            payload.TransactionId,
            payload.PaymentDate,
            payload.Remarks
        );

        if (result.IsSuccess) return Ok(new { status = "Success" });
        
        // Return 200 even on failure to avoid external system retrying indefinitely if it's our logic error,
        // but let's return 400 for bad data so they know.
        return BadRequest(new { status = "Failed", message = result.Error });
    }
}

public class SyncDailyBatchRequest
{
    public DateOnly BusinessDate { get; set; }
}

public class VirtualAccountWebhookPayload
{
    public string VirtualAccountNo { get; set; } = default!;
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = default!;
    public DateOnly PaymentDate { get; set; }
    public string Remarks { get; set; } = default!;
}
