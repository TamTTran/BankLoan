using Microsoft.EntityFrameworkCore;
using LoanFlow.Infrastructure.Data;
using LoanFlow.Application.Common;
using System;
using System.Threading.Tasks;
using System.Linq;
using LoanFlow.Domain.Entities;
using LoanFlow.Domain.Constants;

namespace LoanFlow.Application.Services;

public interface IBodService
{
    Task<Result> RunBodAsync(DateOnly newBusinessDate, int? userId = null);
}

public class BodService : IBodService
{
    private readonly LoanFlowDbContext _db;

    public BodService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Result> RunBodAsync(DateOnly newBusinessDate, int? userId = null)
    {
        // 1. Validate GL Trial Balance = 0 for the previous/current days
        var glAccounts = await _db.GLAccounts.ToListAsync();
        var totalDebits = glAccounts.Where(a => a.AccountType == "Asset" || a.AccountType == "Expense").Sum(a => a.Balance);
        var totalCredits = glAccounts.Where(a => a.AccountType == "Liability" || a.AccountType == "Equity" || a.AccountType == "Revenue").Sum(a => a.Balance);
        if (totalDebits != totalCredits)
        {
            return Result.Failure($"Cannot run BOD. Trial balance mismatch: Assets/Expenses {totalDebits:N2} != Liab/Eq/Rev {totalCredits:N2}");
        }

        // 2. Advance Current Business Date in SystemConfigs
        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "CurrentBusinessDate");
        if (config == null)
        {
            config = new SystemConfig { Key = "CurrentBusinessDate" };
            _db.SystemConfigs.Add(config);
        }
        config.Value = newBusinessDate.ToString("yyyy-MM-dd");
        config.UpdatedAt = DateTime.UtcNow;

        // 3. Unlock System
        var lockConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "IsEodRunning");
        if (lockConfig == null)
        {
            lockConfig = new SystemConfig { Key = "IsEodRunning" };
            _db.SystemConfigs.Add(lockConfig);
        }
        lockConfig.Value = "false";
        lockConfig.UpdatedAt = DateTime.UtcNow;

        // 4. Reset EOD batch
        var latestEod = await _db.EodBatchRecords.OrderByDescending(e => e.BusinessDate).FirstOrDefaultAsync();
        if (latestEod != null && latestEod.Status != "Completed")
        {
            return Result.Failure($"Cannot run BOD. EOD for {latestEod.BusinessDate} is still {latestEod.Status}.");
        }

        // Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = "BodCompleted",
            EntityType = "BOD",
            NewValuesJson = AuditHelper.ToJson(new { newBusinessDate })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }
}
