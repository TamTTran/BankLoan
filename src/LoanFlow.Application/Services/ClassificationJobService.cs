using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public interface IClassificationJobService
{
    Task RunDailyClassificationAsync(DateOnly businessDate, CancellationToken ct = default);
}

public class ClassificationJobService : IClassificationJobService
{
    private readonly LoanFlowDbContext _db;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<ClassificationJobService> _logger;

    public ClassificationJobService(LoanFlowDbContext db, IAccountingService accountingService, ILogger<ClassificationJobService> logger)
    {
        _db = db;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task RunDailyClassificationAsync(DateOnly businessDate, CancellationToken ct = default)
    {
        var todaySys = businessDate.ToDateTime(TimeOnly.MinValue);
        _logger.LogInformation($"[EOD] Starting Loan Classification & Provisioning for {businessDate}");

        var activeLoans = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LoanCollaterals)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToListAsync(ct);

        int updateCount = 0;

        foreach (var loan in activeLoans)
        {
            var unpaidSchedules = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid && s.DueDate.ToDateTime(TimeOnly.MinValue) < todaySys)
                .ToList();

            int maxDpd = 0;
            if (unpaidSchedules.Any())
            {
                var oldestDue = unpaidSchedules.Min(s => s.DueDate.ToDateTime(TimeOnly.MinValue));
                maxDpd = (int)(todaySys - oldestDue).TotalDays;
            }

            int newClass = LoanClassification.Group1_Standard;
            decimal specificProvisionRatio = 0;

            if (maxDpd >= 360) { newClass = LoanClassification.Group5_Loss; specificProvisionRatio = 1.0m; }
            else if (maxDpd >= 180) { newClass = LoanClassification.Group4_Doubtful; specificProvisionRatio = 0.50m; }
            else if (maxDpd >= 90) { newClass = LoanClassification.Group3_Substandard; specificProvisionRatio = 0.20m; }
            else if (maxDpd >= 10) { newClass = LoanClassification.Group2_SpecialMention; specificProvisionRatio = 0.05m; }

            // Lấy giá trị tài sản bảo đảm (Giả định chiết khấu 50% rủi ro thanh khoản theo chuẩn an toàn)
            var collateralValue = loan.LoanCollaterals.Sum(lc => lc.AllocatedValue) * 0.5m;
            
            // 3. Dự phòng Cụ thể (Specific Provision) = max(0, Outstanding - Collateral) * Ratio
            var specificProvision = Math.Round(Math.Max(0, loan.OutstandingAmount - collateralValue) * specificProvisionRatio, 2);

            // 4. Dự phòng Chung (General Provision) = 0.75% * Outstanding (Không áp dụng cho nhóm 5)
            decimal generalProvision = 0;
            if (newClass != LoanClassification.Group5_Loss)
            {
                generalProvision = Math.Round(loan.OutstandingAmount * 0.0075m, 2);
            }

            var requiredProvision = specificProvision + generalProvision;
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

                var journalLines = new List<JournalEntryLineDto>();
                if (deltaProvision > 0)
                {
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "5001", DebitAmount = deltaProvision, CreditAmount = 0, LineDescription = "Provision Expense" });
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "1309", DebitAmount = 0, CreditAmount = deltaProvision, LineDescription = "Allowance for Loan Losses" });
                }
                else
                {
                    var absDelta = Math.Abs(deltaProvision);
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "1309", DebitAmount = absDelta, CreditAmount = 0, LineDescription = "Allowance for Loan Losses Reversal" });
                    journalLines.Add(new JournalEntryLineDto { AccountCode = "5001", DebitAmount = 0, CreditAmount = absDelta, LineDescription = "Provision Expense Reversal" });
                }

                var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
                {
                    ReferenceNumber = $"PROV-{loan.Id}-{businessDate:yyyyMMdd}",
                    BusinessDate = businessDate,
                    Description = $"Provision Adjustment for Loan {loan.LoanAccountNo} (Group {newClass})",
                    Lines = journalLines
                });

                if (!journalResult.IsSuccess)
                    _logger.LogWarning($"Failed to post provision journal for loan {loan.Id}: {journalResult.Error}");
            }

            if (changed)
            {
                loan.UpdatedAt = DateTime.UtcNow;
                updateCount++;
            }

            _db.Set<ClassificationSnapshot>().Add(new ClassificationSnapshot
            {
                LoanId = loan.Id,
                SnapshotDate = businessDate,
                Classification = loan.CurrentClassification,
                OutstandingAmount = loan.OutstandingAmount,
                ProvisionAmount = loan.ProvisionAmount,
                Dpd = maxDpd
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation($"[EOD] Classification finished. Updated {updateCount} loans. {activeLoans.Count} snapshots written.");
    }
}
