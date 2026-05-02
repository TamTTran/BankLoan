using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public class CoreBankingIntegrationService : ICoreBankingIntegrationService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<CoreBankingIntegrationService> _logger;

    public CoreBankingIntegrationService(LoanFlowDbContext db, ILogger<CoreBankingIntegrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> PushJournalEntryAsync(int journalEntryId)
    {
        var je = await _db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == journalEntryId);

        if (je == null) return Result.Failure("Journal Entry not found");

        if (je.SyncStatus == "Synced") return Result.Failure("Already synced");

        try
        {
            // Simulate API call to Core Banking (e.g. T24 / Flexcube)
            _logger.LogInformation($"Pushing JE {je.ReferenceNumber} to Core Banking...");
            
            // In real world, we map je.Lines to the Core Banking API format
            await Task.Delay(100); // Simulate network latency

            je.SyncStatus = "Synced";
            je.SyncedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to push JE {je.ReferenceNumber} to Core Banking");
            je.SyncStatus = "Failed";
            await _db.SaveChangesAsync();
            return Result.Failure($"Sync failed: {ex.Message}");
        }
    }

    public async Task<Result> PushDailyBatchAsync(DateOnly businessDate)
    {
        var pendingEntries = await _db.JournalEntries
            .Where(j => j.BusinessDate == businessDate && j.SyncStatus == "Pending")
            .ToListAsync();

        int successCount = 0;
        int failCount = 0;

        foreach (var entry in pendingEntries)
        {
            var result = await PushJournalEntryAsync(entry.Id);
            if (result.IsSuccess) successCount++;
            else failCount++;
        }

        _logger.LogInformation($"Core Banking Daily Sync for {businessDate}: {successCount} Success, {failCount} Failed");

        return failCount == 0 ? Result.Success() : Result.Failure($"{failCount} entries failed to sync");
    }
}
