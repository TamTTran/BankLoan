using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILateFeeService
{
    Task<Result> CalculateLateFeesAsync(int loanId, DateOnly calculationDate);
    Task<Result> CalculateLateFeesAsync(Loan loan, DateOnly calculationDate);
    Task<Result> WaiveLateFeeAsync(int lateFeeId, int userId);
}

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

        foreach (var schedule in loan.RepaymentSchedules)
        {
            // Only consider unpaid or partially paid installments that are past due date
            if (schedule.Status == InstallmentStatus.Paid) continue;
            if (schedule.DueDate >= calculationDate) continue;

            int daysOverdue = calculationDate.DayNumber - schedule.DueDate.DayNumber;
            
            // Only calculate if past the grace days
            if (daysOverdue <= loan.LateFeeGraceDays) continue;

            // Late fee formula: overdue_amount * rate * effective_days / 360
            // We use the current remaining amount as overdue amount for calculation. 
            // In a real bank, this can be complex (e.g. tracking historical balances), but we use current remaining due.
            decimal overdueAmount = schedule.RemainingDueAmount;
            if (overdueAmount <= 0) continue;

            // Annual rate applied daily
            decimal dailyRate = loan.LateFeeRate / 360m;
            int effectiveDays = daysOverdue - loan.LateFeeGraceDays;
            decimal calculatedFee = Math.Round(overdueAmount * dailyRate * effectiveDays, 2);

            var existingFee = loan.LateFees.FirstOrDefault(f => f.ScheduleId == schedule.Id && f.Status == LateFeeStatus.Pending);
            
            var accrualDelta = 0m;

            if (existingFee != null)
            {
                var difference = calculatedFee - existingFee.FeeAmount;
                if (difference > 0)
                {
                    accrualDelta = difference;
                    // Update existing pending fee
                    existingFee.OverdueAmount = overdueAmount;
                    existingFee.DaysOverdue = daysOverdue;
                    existingFee.FeeAmount = calculatedFee;
                    existingFee.CalculatedAt = _businessClock.UtcNow;
                }
            }
            else
            {
                accrualDelta = calculatedFee;
                // Create new pending fee
                var newFee = new LateFee
                {
                    LoanId = loan.Id,
                    ScheduleId = schedule.Id,
                    OverdueAmount = overdueAmount,
                    DaysOverdue = daysOverdue,
                    FeeRate = loan.LateFeeRate,
                    FeeAmount = calculatedFee,
                    Status = LateFeeStatus.Pending,
                    CalculatedAt = _businessClock.UtcNow
                };
                _db.LateFees.Add(newFee);

                // Ledger chain: create a LoanTransaction record for this accrual
                var accrualTx = new LoanTransaction
                {
                    LoanId = loan.Id,
                    PaymentId = null,
                    TransactionType = "LateFeeAccrual",
                    Amount = calculatedFee,
                    ReferenceNo = $"LATE-{loan.Id}-S{schedule.Id}"
                };
                _db.LoanTransactions.Add(accrualTx);

                // Back-link: we'll update AccrualLoanTransactionId after SaveChanges
                await _db.SaveChangesAsync();
                newFee.AccrualLoanTransactionId = accrualTx.Id;
            }

            if (accrualDelta > 0)
            {
                var debitCode = await _accountingService.GetAccountCodeAsync("LateFee_Accrual", true);
                var creditCode = await _accountingService.GetAccountCodeAsync("LateFee_Accrual", false);

                var journalResult = await _accountingService.PostJournalAsync(new JournalEntryDto
                {
                    ReferenceNumber = $"LATEFEE-ACCR-{loan.Id}-S{schedule.Id}-{_businessClock.TodayBusinessDate():yyMMddHHmmss}",
                    BusinessDate = _businessClock.TodayBusinessDate(),
                    Description = $"Late Fee Accrual for Loan {loan.LoanAccountNo}",
                    Lines = new List<JournalEntryLineDto>
                    {
                        new JournalEntryLineDto { AccountCode = debitCode, DebitAmount = accrualDelta, CreditAmount = 0, LineDescription = "Late Fee Receivable" },
                        new JournalEntryLineDto { AccountCode = creditCode, DebitAmount = 0, CreditAmount = accrualDelta, LineDescription = "Late Fee Income" }
                    }
                });
                
                if (!journalResult.IsSuccess)
                {
                    throw new DomainValidationException($"Failed to post Late Fee Journal: {journalResult.Error}");
                }
            }
        }

        await SaveChangesWithConcurrencyHandlingAsync();
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
