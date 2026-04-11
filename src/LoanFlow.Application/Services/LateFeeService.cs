using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILateFeeService
{
    Task<Result> CalculateLateFeesAsync(int loanId, DateOnly calculationDate);
    Task<Result> WaiveLateFeeAsync(int lateFeeId, int userId);
}

public class LateFeeService : ILateFeeService
{
    private readonly LoanFlowDbContext _db;

    public LateFeeService(LoanFlowDbContext db) => _db = db;

    public async Task<Result> CalculateLateFeesAsync(int loanId, DateOnly calculationDate)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure("Loan not found.");
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
            
            if (existingFee != null)
            {
                // Update existing pending fee
                existingFee.OverdueAmount = overdueAmount;
                existingFee.DaysOverdue = daysOverdue;
                existingFee.FeeAmount = calculatedFee;
                existingFee.CalculatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new pending fee
                _db.LateFees.Add(new LateFee
                {
                    LoanId = loanId,
                    ScheduleId = schedule.Id,
                    OverdueAmount = overdueAmount,
                    DaysOverdue = daysOverdue,
                    FeeRate = loan.LateFeeRate,
                    FeeAmount = calculatedFee,
                    Status = LateFeeStatus.Pending,
                    CalculatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
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
            NewValuesJson = $"{{\"waivedAmount\":{lateFee.FeeAmount}}}"
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }
}
