using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LoanFlow.Application.Services;

namespace LoanFlow.API.BackgroundJobs;

public class DailyAccrualBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyAccrualBackgroundService> _logger;

    public DailyAccrualBackgroundService(IServiceProvider serviceProvider, ILogger<DailyAccrualBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyAccrualBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing DailyAccrualBackgroundService");
            }

            // In production, this would be computed to exactly midnight.
            // For testing, repeating every few hours or via a trigger is better, but we will put 24h.
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IBusinessClock>();
        var _accountingService = scope.ServiceProvider.GetRequiredService<IAccountingService>();

        var today = clock.TodayBusinessDate();

        _logger.LogInformation($"[EOD] Starting Daily Interest Accrual for {today}");

        // Find active or overdue loans (where interest still accrues)
        var loansToAccrue = await db.Loans
            .Where(l => (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured) && l.OutstandingAmount > 0)
            .ToListAsync(token);

        int count = 0;
        decimal totalAccrued = 0;

        foreach (var loan in loansToAccrue)
        {
            // Simple daily accrual formula: Outstanding * MonthlyRate * 12 / 365 
            // Depending on bank policy, some banks use exact days (Actual/365).
            // For this project, we assume MonthlyInterestRate * 12 / 365.
            var dailyRate = (loan.MonthlyInterestRate * 12) / 365m;
            var dailyAccrual = Math.Round(loan.OutstandingAmount * dailyRate, 2);

            if (dailyAccrual <= 0) continue;

            totalAccrued += dailyAccrual;
            count++;

            var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
            {
                ReferenceNumber = $"ACCR-{loan.Id}-{today:yyyyMMdd}",
                BusinessDate = today,
                Description = $"Daily Interest Accrual for Loan {loan.LoanAccountNo}",
                Lines = new List<JournalEntryLineDto>
                {
                    // Debit 1302 (Interest Receivable)
                    new JournalEntryLineDto { AccountCode = "1302", DebitAmount = dailyAccrual, CreditAmount = 0, LineDescription = "Interest Accrued Receivable" },
                    // Credit 4001 (Interest Income)
                    new JournalEntryLineDto { AccountCode = "4001", DebitAmount = 0, CreditAmount = dailyAccrual, LineDescription = "Interest Income Extracted" }
                }
            });

            if (!journalResult.IsSuccess)
            {
                _logger.LogWarning($"Failed to post accrual for loan {loan.Id}: {journalResult.Error}");
            }
        }

        await db.SaveChangesAsync(token);
        
        _logger.LogInformation($"[EOD] Accrual finished: {count} loans, total Amount: {totalAccrued:N2}");
    }
}
