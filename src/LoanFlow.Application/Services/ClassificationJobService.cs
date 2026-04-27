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
                .ThenInclude(lc => lc.Collateral)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToListAsync(ct);

        int updateCount = 0;

        // Pass 1: Calculate provisional classifications and max per customer
        var customerMaxClass = new Dictionary<int, int>();
        var loanMaxDpd = new Dictionary<int, int>();

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
            loanMaxDpd[loan.Id] = maxDpd;

            int provClass = LoanClassification.Group1_Standard;
            if (maxDpd > 360) provClass = LoanClassification.Group5_Loss;
            else if (maxDpd >= 181) provClass = LoanClassification.Group4_Doubtful;
            else if (maxDpd >= 91) provClass = LoanClassification.Group3_Substandard;
            else if (maxDpd >= 10) provClass = LoanClassification.Group2_SpecialMention;

            if (loan.QualitativeClassification.HasValue && loan.QualitativeClassification.Value > provClass)
            {
                provClass = loan.QualitativeClassification.Value;
            }

            if (!customerMaxClass.ContainsKey(loan.CustomerId) || provClass > customerMaxClass[loan.CustomerId])
            {
                customerMaxClass[loan.CustomerId] = provClass;
            }
        }

        // Pass 2: Apply cross-classification and compute provisions
        foreach (var loan in activeLoans)
        {
            int maxDpd = loanMaxDpd[loan.Id];
            
            // Áp dụng lây nhiễm nhóm nợ (Cross-Contagion)
            int newClass = customerMaxClass[loan.CustomerId]; 

            decimal specificProvisionRatio = newClass switch
            {
                LoanClassification.Group5_Loss => 1.0m,
                LoanClassification.Group4_Doubtful => 0.50m,
                LoanClassification.Group3_Substandard => 0.20m,
                LoanClassification.Group2_SpecialMention => 0.05m,
                _ => 0.0m
            };

            // Lấy giá trị tài sản bảo đảm đã chiết khấu (Haircut)
            decimal collateralValue = 0;
            foreach (var lc in loan.LoanCollaterals)
            {
                decimal haircut = lc.Collateral.CollateralType switch
                {
                    CollateralType.RealEstate => CollateralHaircutRate.RealEstate,
                    CollateralType.Vehicle => CollateralHaircutRate.Vehicle,
                    CollateralType.SavingsBook => CollateralHaircutRate.SavingsBook,
                    _ => CollateralHaircutRate.Other
                };
                collateralValue += lc.AllocatedValue * haircut;
            }
            
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
