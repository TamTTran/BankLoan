using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LoanFlow.Application.Services;

namespace LoanFlow.API.BackgroundJobs;

public class DailyClassificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyClassificationBackgroundService> _logger;

    public DailyClassificationBackgroundService(IServiceProvider serviceProvider, ILogger<DailyClassificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyClassificationBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing DailyClassificationBackgroundService");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IBusinessClock>();
        var accountingService = scope.ServiceProvider.GetRequiredService<IAccountingService>();

        var today = clock.TodayBusinessDate();
        var todaySys = today.ToDateTime(TimeOnly.MinValue);

        _logger.LogInformation($"[EOD] Starting Loan Classification & Provisioning for {today}");

        var activeLoans = await db.Loans
            .Include(l => l.RepaymentSchedules)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToListAsync(token);

        int updateCount = 0;

        foreach (var loan in activeLoans)
        {
            // 1. Calculate Max DPD
            var unpaidSchedules = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid && s.DueDate.ToDateTime(TimeOnly.MinValue) < todaySys)
                .ToList();

            int maxDpd = 0;
            if (unpaidSchedules.Any())
            {
                var oldestDue = unpaidSchedules.Min(s => s.DueDate.ToDateTime(TimeOnly.MinValue));
                maxDpd = (int)(todaySys - oldestDue).TotalDays;
            }

            // 2. Determine Classification and Ratio
            int newClass = LoanClassification.Group1_Standard;
            decimal provisionRatio = 0;

            if (maxDpd >= 360)
            {
                newClass = LoanClassification.Group5_Loss;
                provisionRatio = 1.0m; // 100%
            }
            else if (maxDpd >= 180)
            {
                newClass = LoanClassification.Group4_Doubtful;
                provisionRatio = 0.50m; // 50%
            }
            else if (maxDpd >= 90)
            {
                newClass = LoanClassification.Group3_Substandard;
                provisionRatio = 0.20m; // 20%
            }
            else if (maxDpd >= 10)
            {
                newClass = LoanClassification.Group2_SpecialMention;
                provisionRatio = 0.05m; // 5%
            }

            // 3. Calculate Required Provision
            var requiredProvision = Math.Round(loan.OutstandingAmount * provisionRatio, 2);
            var deltaProvision = requiredProvision - loan.ProvisionAmount;

            bool changed = false;

            if (loan.CurrentClassification != newClass)
            {
                loan.CurrentClassification = newClass;
                changed = true;
            }

            if (deltaProvision != 0)
            {
                loan.ProvisionAmount = requiredProvision;
                changed = true;

                // Post Accounting Journal for Provision Change
                var journalLines = new List<JournalEntryLineDto>();
                if (deltaProvision > 0)
                {
                    // Expense Increase
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "5001", DebitAmount = deltaProvision, CreditAmount = 0, LineDescription = "Provision Expense" });
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "1309", DebitAmount = 0, CreditAmount = deltaProvision, LineDescription = "Allowance for Loan Losses" });
                }
                else
                {
                    // Reversal (Expense Decrease)
                    var absDelta = Math.Abs(deltaProvision);
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "1309", DebitAmount = absDelta, CreditAmount = 0, LineDescription = "Allowance for Loan Losses Reversal" });
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "5001", DebitAmount = 0, CreditAmount = absDelta, LineDescription = "Provision Expense Reversal" });
                }

                var journalResult = await accountingService.PostJournalAsync(new JournalEntryDto
                {
                    ReferenceNumber = $"PROV-{loan.Id}-{today:yyyyMMdd}",
                    BusinessDate = today,
                    Description = $"Provision Adjustment for Loan {loan.LoanAccountNo} (Group {newClass})",
                    Lines = journalLines
                });

                if (!journalResult.IsSuccess)
                {
                    _logger.LogWarning($"Failed to post provision journal for loan {loan.Id}: {journalResult.Error}");
                }
            }

            if (changed)
            {
                loan.UpdatedAt = DateTime.UtcNow;
                updateCount++;
            }

            // Write classification snapshot for NPL migration matrix
            db.Set<ClassificationSnapshot>().Add(new ClassificationSnapshot
            {
                LoanId = loan.Id,
                SnapshotDate = today,
                Classification = loan.CurrentClassification,
                OutstandingAmount = loan.OutstandingAmount,
                ProvisionAmount = loan.ProvisionAmount,
                Dpd = maxDpd
            });
        }

        await db.SaveChangesAsync(token);

        _logger.LogInformation($"[EOD] Classification finished. Updated {updateCount} loans. {activeLoans.Count} snapshots written.");
    }
}
