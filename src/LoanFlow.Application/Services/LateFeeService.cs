using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class LateFeeService : ILateFeeService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessClock _businessClock;
    private readonly IAccountingService _accountingService;

    public LateFeeService(LoanFlowDbContext db, IBusinessClock businessClock, IAccountingService accountingService)
    {
        _db = db;
        _businessClock = businessClock;
        _accountingService = accountingService;
    }

    public async Task<Result> CalculateLateFeesAsync(int loanId, DateOnly calculationDate)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure("Loan not found.");
        return await CalculateLateFeesAsync(loan, calculationDate);
    }

    public async Task<Result> CalculateLateFeesAsync(Loan loan, DateOnly calculationDate)
    {
        if (loan.Status == LoanStatus.Closed) return Result.Success();

        // Ensure rate exists (> 0). Some products might not have late fee enabled.
        if (loan.LateFeeRate <= 0) return Result.Success();

        // Pass 1: Collect all new LateFee + LoanTransaction pairs without SaveChanges per item (BUG-005 fix).
        var newFeeEntries = new List<(LateFee Fee, LoanTransaction Tx)>();

        var debitCode  = await _accountingService.GetAccountCodeAsync("LateFee_Accrual", true);
        var creditCode = await _accountingService.GetAccountCodeAsync("LateFee_Accrual", false);

        foreach (var schedule in loan.RepaymentSchedules)
        {
            if (schedule.Status == InstallmentStatus.Paid) continue;
            if (schedule.DueDate >= calculationDate) continue;

            int daysOverdue = calculationDate.DayNumber - schedule.DueDate.DayNumber;
            if (daysOverdue <= loan.LateFeeGraceDays) continue;

            if (schedule.RemainingDueAmount <= 0) continue;

            int effectiveDays = daysOverdue - loan.LateFeeGraceDays;
            decimal dailyRate = loan.LateFeeRate / 360m;

            var existingFee = loan.LateFees.FirstOrDefault(f => f.ScheduleId == schedule.Id && f.Status == LateFeeStatus.Pending);

            decimal accrualDelta;

            if (existingFee != null)
            {
                // BUG-001 fix: use frozen BaseOverdueAmount so partial payments don't corrupt accrual math.
                var recalculated = Math.Round(existingFee.BaseOverdueAmount * dailyRate * effectiveDays, 2);
                accrualDelta = recalculated - existingFee.FeeAmount;

                if (accrualDelta > 0)
                {
                    existingFee.OverdueAmount = schedule.RemainingDueAmount;
                    existingFee.DaysOverdue   = daysOverdue;
                    existingFee.FeeAmount     = recalculated;
                    existingFee.CalculatedAt  = _businessClock.UtcNow;
                }
                else
                {
                    accrualDelta = 0;
                }
            }
            else
            {
                // Freeze base at grace-period-end (current remaining amount before any future partial payments).
                decimal baseAmount    = schedule.RemainingDueAmount;
                decimal calculatedFee = Math.Round(baseAmount * dailyRate * effectiveDays, 2);
                accrualDelta = calculatedFee;

                var newFee = new LateFee
                {
                    LoanId            = loan.Id,
                    ScheduleId        = schedule.Id,
                    OverdueAmount     = baseAmount,
                    BaseOverdueAmount = baseAmount,
                    DaysOverdue       = daysOverdue,
                    FeeRate           = loan.LateFeeRate,
                    FeeAmount         = calculatedFee,
                    Status            = LateFeeStatus.Pending,
                    CalculatedAt      = _businessClock.UtcNow
                };
                var accrualTx = new LoanTransaction
                {
                    LoanId          = loan.Id,
                    PaymentId       = null,
                    TransactionType = "LateFeeAccrual",
                    Amount          = calculatedFee,
                    ReferenceNo     = $"LATE-{loan.Id}-S{schedule.Id}"
                };

                _db.LateFees.Add(newFee);
                _db.LoanTransactions.Add(accrualTx);
                newFeeEntries.Add((newFee, accrualTx));
            }

            if (accrualDelta > 0)
            {
                var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
                {
                    ReferenceNumber = $"LATEFEE-ACCR-{loan.Id}-S{schedule.Id}-{_businessClock.UtcNow:yyMMddHHmmss}",
                    BusinessDate    = _businessClock.TodayBusinessDate(),
                    Description     = $"Late Fee Accrual for Loan {loan.LoanAccountNo}",
                    Lines = new List<JournalEntryLineDto>
                    {
                        new JournalEntryLineDto { AccountCode = debitCode, DebitAmount = accrualDelta, CreditAmount = 0, LineDescription = "Late Fee Receivable" },
                        new JournalEntryLineDto { AccountCode = creditCode, DebitAmount = 0, CreditAmount = accrualDelta, LineDescription = "Late Fee Income" }
                    }
                });

                if (!journalResult.IsSuccess)
                    throw new DomainValidationException($"Failed to post Late Fee Journal: {journalResult.Error}");
            }
        }

        // Pass 2: Single SaveChanges to get DB-generated IDs for new rows.
        // Callers that own an ambient transaction (e.g. LoanPaymentService) rely on this NOT calling
        // an extra SaveChanges — the back-link update (Pass 3) will be flushed by the caller's own SaveChanges.
        if (newFeeEntries.Count > 0)
        {
            await _db.SaveChangesAsync();

            // Pass 3: Back-link IDs now that EF has populated them; flushed by caller's next SaveChanges.
            foreach (var (fee, tx) in newFeeEntries)
                fee.AccrualLoanTransactionId = tx.Id;
        }

        return Result.Success();
    }

    public async Task<Result> WaiveLateFeeAsync(int lateFeeId, int userId)
    {
        var lateFee = await _db.LateFees.FindAsync(lateFeeId);
        if (lateFee is null) return Result.Failure("Late fee not found.");

        if (lateFee.Status != LateFeeStatus.Pending)
            return Result.Failure("Only pending late fees can be waived.");

        lateFee.Status = LateFeeStatus.Waived;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.LateFeeWaived,
            EntityType = "LateFee",
            EntityId = lateFee.Id,
            NewValuesJson = AuditHelper.ToJson(new { waivedAmount = lateFee.FeeAmount })
        });
        
        // Reverse Late Fee Income
        var debitCode = await _accountingService.GetAccountCodeAsync("LateFee_Waiver", true);
        var creditCode = await _accountingService.GetAccountCodeAsync("LateFee_Waiver", false);

        var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
        {
            ReferenceNumber = $"LATEWF-{lateFee.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            BusinessDate = _businessClock.TodayBusinessDate(),
            Description = $"Late Fee Waived for LateFeeId {lateFee.Id}",
            Lines = new List<JournalEntryLineDto>
            {
                new JournalEntryLineDto { AccountCode = debitCode, DebitAmount = lateFee.FeeAmount, CreditAmount = 0, LineDescription = "Late Fee Income Reversal" },
                new JournalEntryLineDto { AccountCode = creditCode, DebitAmount = 0, CreditAmount = lateFee.FeeAmount, LineDescription = "Late Fee Receivable Reversal" }
            }
        });

        if (!journalResult.IsSuccess)
        {
            throw new DomainValidationException($"Failed to post Late Fee Waiver Journal: {journalResult.Error}");
        }

        await SaveChangesWithConcurrencyHandlingAsync();
        return Result.Success();
    }

    private async Task SaveChangesWithConcurrencyHandlingAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Late fee data was modified by another transaction. Reload and retry.");
        }
    }
}
