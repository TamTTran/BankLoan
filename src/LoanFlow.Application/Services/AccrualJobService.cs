using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public interface IAccrualJobService
{
    Task RunDailyAccrualAsync(DateOnly businessDate, CancellationToken ct = default);
}

public class AccrualJobService : IAccrualJobService
{
    private readonly LoanFlowDbContext _db;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<AccrualJobService> _logger;

    public AccrualJobService(LoanFlowDbContext db, IAccountingService accountingService, ILogger<AccrualJobService> logger)
    {
        _db = db;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task RunDailyAccrualAsync(DateOnly businessDate, CancellationToken ct = default)
    {
        _logger.LogInformation($"[EOD] Starting Daily Interest Accrual for {businessDate}");

        // Find active or overdue loans (where interest still accrues)
        var loansToAccrue = await _db.Loans
            .Where(l => (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured) && l.OutstandingAmount > 0)
            .ToListAsync(ct);

        int count = 0;
        decimal totalAccrued = 0;

        foreach (var loan in loansToAccrue)
        {
            var dailyRate = (loan.MonthlyInterestRate * 12) / 365m;
            var dailyAccrual = Math.Round(loan.OutstandingAmount * dailyRate, 2);

            if (dailyAccrual <= 0) continue;

            totalAccrued += dailyAccrual;
            count++;

            var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
            {
                ReferenceNumber = $"ACCR-{loan.Id}-{businessDate:yyyyMMdd}",
                BusinessDate = businessDate,
                Description = $"Daily Interest Accrual for Loan {loan.LoanAccountNo}",
                Lines = new List<JournalEntryLineDto>
                {
                    new JournalEntryLineDto { AccountCode = "1302", DebitAmount = dailyAccrual, CreditAmount = 0, LineDescription = "Interest Accrued Receivable" },
                    new JournalEntryLineDto { AccountCode = "4001", DebitAmount = 0, CreditAmount = dailyAccrual, LineDescription = "Interest Income Extracted" }
                }
            });

            if (!journalResult.IsSuccess)
            {
                _logger.LogWarning($"Failed to post accrual for loan {loan.Id}: {journalResult.Error}");
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation($"[EOD] Accrual finished: {count} loans, total Amount: {totalAccrued:N2}");
    }
}
