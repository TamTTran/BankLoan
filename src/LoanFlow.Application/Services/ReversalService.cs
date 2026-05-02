using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class ReversalService : IReversalService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessClock _businessClock;

    public ReversalService(LoanFlowDbContext db, IBusinessClock businessClock)
    {
        _db = db;
        _businessClock = businessClock;
    }

    public async Task<Result<ReversalRequestDto>> RequestPaymentReversalAsync(int loanId, RequestReversalDto request, int userId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<ReversalRequestDto>("Reversal reason is required.");

        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure<ReversalRequestDto>("Loan not found.");

        // Find the original payment → its transaction
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentId && p.LoanId == loanId);
        if (payment is null)
            return Result.Failure<ReversalRequestDto>("Payment not found for this loan.");

        var originalTx = await _db.LoanTransactions
            .FirstOrDefaultAsync(t => t.PaymentId == payment.Id && t.TransactionType == "Payment");
        if (originalTx is null)
            return Result.Failure<ReversalRequestDto>("Original transaction record not found.");

        // Check no existing pending reversal for this transaction
        var existingReversal = await _db.TransactionReversals
            .AnyAsync(r => r.OriginalTransactionId == originalTx.Id && r.Status == ReversalStatus.Pending);
        if (existingReversal)
            return Result.Failure<ReversalRequestDto>("A pending reversal already exists for this transaction.");

        var reversal = new TransactionReversal
        {
            LoanId = loanId,
            OriginalTransactionId = originalTx.Id,
            ReversalType = Domain.Constants.ReversalType.PaymentReversal,
            OriginalAmount = payment.Amount,
            ReversedAmount = payment.Amount, // full reversal
            Reason = request.Reason,
            Status = ReversalStatus.Pending,
            RequestedByUserId = userId
        };

        _db.TransactionReversals.Add(reversal);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.ReversalRequested,
            EntityType = "TransactionReversal",
            EntityId = 0, // will update after save
            NewValuesJson = AuditHelper.ToJson(new { loanId, paymentId = payment.Id, amount = payment.Amount, reason = request.Reason })
        });

        await _db.SaveChangesAsync();

        return Result.Success(MapToDto(reversal));
    }

    public async Task<Result<ReversalRequestDto>> ApproveReversalAsync(int reversalId, int approverUserId)
    {
        var reversal = await _db.TransactionReversals
            .Include(r => r.OriginalTransaction)
            .FirstOrDefaultAsync(r => r.Id == reversalId);

        if (reversal is null) return Result.Failure<ReversalRequestDto>("Reversal request not found.");
        if (reversal.Status != ReversalStatus.Pending)
            return Result.Failure<ReversalRequestDto>("Only pending reversals can be approved.");

        // Prevent self-approval
        if (reversal.RequestedByUserId == approverUserId)
            return Result.Failure<ReversalRequestDto>("Cannot approve your own reversal request.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var loan = await _db.Loans
                .Include(l => l.RepaymentSchedules)
                .Include(l => l.LateFees)
                .AsSplitQuery()
                .FirstOrDefaultAsync(l => l.Id == reversal.LoanId);

            if (loan is null) return Result.Failure<ReversalRequestDto>("Loan not found.");

            // Find the original payment
            var originalPayment = await _db.Payments
                .FirstOrDefaultAsync(p => p.LoanId == reversal.LoanId
                    && _db.LoanTransactions.Any(t => t.Id == reversal.OriginalTransactionId && t.PaymentId == p.Id));

            if (originalPayment is null)
                return Result.Failure<ReversalRequestDto>("Original payment not found.");

            // 1. Create compensating (negative) payment
            var compensatingPayment = new Payment
            {
                LoanId = reversal.LoanId,
                PaymentDate = _businessClock.TodayBusinessDate(),
                Amount = -reversal.ReversedAmount,
                PaymentMethod = originalPayment.PaymentMethod,
                Remarks = $"Reversal of Payment #{originalPayment.Id}: {reversal.Reason}",
                CreatedByUserId = approverUserId
            };
            _db.Payments.Add(compensatingPayment);
            await SaveChangesWithConcurrencyHandlingAsync("Reversal approval conflicted with another transaction. Reload and retry."); // get compensatingPayment.Id

            // 2. Create compensating transaction
            var compensatingTx = new LoanTransaction
            {
                LoanId = reversal.LoanId,
                PaymentId = compensatingPayment.Id,
                TransactionType = "PaymentReversal",
                Amount = -reversal.ReversedAmount,
                ReferenceNo = $"REV-{reversal.Id}"
            };
            _db.LoanTransactions.Add(compensatingTx);
            await SaveChangesWithConcurrencyHandlingAsync("Reversal approval conflicted with another transaction. Reload and retry.");

            // ── Ledger Chain: write negative-mirror PaymentAllocations for the reversal ──
            // Query original allocations to know exactly what components to reverse
            var originalAllocations = await _db.PaymentAllocations
                .Where(a => a.LoanTransactionId == reversal.OriginalTransactionId)
                .ToListAsync();

            if (originalAllocations.Count > 0)
            {
                // Mirror each original allocation with a negative record linked to the compensating tx
                foreach (var orig in originalAllocations)
                {
                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        PaymentId        = compensatingPayment.Id,
                        LoanTransactionId = compensatingTx.Id,
                        ScheduleId       = orig.ScheduleId,
                        LateFeeId        = orig.LateFeeId,
                        AllocationType   = orig.AllocationType,   // same type (Principal/Interest/LateFee/Suspense)
                        Amount           = -orig.Amount           // negative = reversal
                    });
                }
            }
            else
            {
                // Fallback: no allocation records found (pre-ledger payments) — write a single Reversal entry
                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId        = compensatingPayment.Id,
                    LoanTransactionId = compensatingTx.Id,
                    ScheduleId       = null,
                    AllocationType   = "Reversal",
                    Amount           = -reversal.ReversedAmount
                });
            }

            // 3. Roll back schedule allocations
            //    We need to "undo" what the original payment did.
            //    Strategy: add back the reversed amount using reverse allocation (Principal → Interest → LateFee)
            var amountToRestore = reversal.ReversedAmount;

            // Reverse order: start from newest paid installments
            var schedules = loan.RepaymentSchedules
                .OrderByDescending(s => s.InstallmentNo)
                .ToList();

            foreach (var schedule in schedules)
            {
                if (amountToRestore <= 0) break;

                // Restore principal first (reverse of payment allocation order)
                var principalToRestore = Math.Min(amountToRestore, schedule.PrincipalPaid);
                if (principalToRestore > 0)
                {
                    schedule.PrincipalPaid -= principalToRestore;
                    amountToRestore -= principalToRestore;
                }

                // Then restore interest
                var interestToRestore = Math.Min(amountToRestore, schedule.InterestPaid);
                if (interestToRestore > 0)
                {
                    schedule.InterestPaid -= interestToRestore;
                    amountToRestore -= interestToRestore;
                }

                // Recalculate remaining due
                schedule.RemainingDueAmount = (schedule.PrincipalDue - schedule.PrincipalPaid)
                                            + (schedule.InterestDue - schedule.InterestPaid);

                // Update status
                if (schedule.RemainingDueAmount > 0 && schedule.PrincipalPaid + schedule.InterestPaid > 0)
                    schedule.Status = InstallmentStatus.Partial;
                else if (schedule.RemainingDueAmount > 0)
                    schedule.Status = InstallmentStatus.Unpaid;
                // else keep as Paid
            }

            // 4. Recalculate loan outstanding
            loan.OutstandingAmount = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.RemainingDueAmount);

            // 5. If loan was Closed and now has outstanding → revert to Active
            if (loan.Status == LoanStatus.Closed && loan.OutstandingAmount > 0)
            {
                loan.Status = LoanStatus.Active;
            }

            loan.UpdatedAt = _businessClock.UtcNow;

            // 6. Update reversal record
            reversal.ReversalTransactionId = compensatingTx.Id;
            reversal.Status = ReversalStatus.Approved;
            reversal.ApprovedByUserId = approverUserId;
            reversal.ApprovedAt = _businessClock.UtcNow;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = approverUserId,
                ActionCode = AuditActions.ReversalApproved,
                EntityType = "TransactionReversal",
                EntityId = reversal.Id,
                NewValuesJson = AuditHelper.ToJson(new { reversalId, loanId = reversal.LoanId, reversedAmount = reversal.ReversedAmount })
            });

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = approverUserId,
                ActionCode = AuditActions.PaymentReversed,
                EntityType = "Payment",
                EntityId = originalPayment.Id,
                NewValuesJson = AuditHelper.ToJson(new { originalAmount = reversal.OriginalAmount, reversedAmount = reversal.ReversedAmount, newOutstanding = loan.OutstandingAmount })
            });

            await SaveChangesWithConcurrencyHandlingAsync("Reversal approval conflicted with another transaction. Reload and retry.");
            await tx.CommitAsync();

            return Result.Success(MapToDto(reversal));
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            throw new ConcurrencyConflictException("Reversal approval conflicted with another transaction. Reload and retry.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Result> RejectReversalAsync(int reversalId, string reason, int approverUserId)
    {
        var reversal = await _db.TransactionReversals.FindAsync(reversalId);
        if (reversal is null) return Result.Failure("Reversal request not found.");
        if (reversal.Status != ReversalStatus.Pending)
            return Result.Failure("Only pending reversals can be rejected.");

        reversal.Status = ReversalStatus.Rejected;
        reversal.ApprovedByUserId = approverUserId;
        reversal.ApprovedAt = _businessClock.UtcNow;
        reversal.Reason += $" | Rejected: {reason}";

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = approverUserId,
            ActionCode = AuditActions.ReversalRejected,
            EntityType = "TransactionReversal",
            EntityId = reversal.Id,
            NewValuesJson = AuditHelper.ToJson(new { reversalId, reason })
        });

        await SaveChangesWithConcurrencyHandlingAsync("Reversal rejection conflicted with another transaction. Reload and retry.");
        return Result.Success();
    }

    public async Task<Result> AdjustFeeAsync(int loanId, AdjustFeeDto request, int userId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure("Adjustment reason is required.");

        var fee = await _db.LateFees.FirstOrDefaultAsync(f => f.Id == request.LateFeeId && f.ScheduleId != 0);
        if (fee is null) return Result.Failure("Late fee not found.");
        if (fee.Status != LateFeeStatus.Pending)
            return Result.Failure("Only pending fees can be adjusted.");

        var oldAmount = fee.FeeAmount;
        fee.FeeAmount = request.NewAmount;

        if (request.NewAmount == 0)
            fee.Status = LateFeeStatus.Waived;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.FeeAdjusted,
            EntityType = "LateFee",
            EntityId = fee.Id,
            NewValuesJson = AuditHelper.ToJson(new { loanId, oldAmount, newAmount = request.NewAmount, reason = request.Reason })
        });

        await SaveChangesWithConcurrencyHandlingAsync("Fee adjustment conflicted with another transaction. Reload and retry.");
        return Result.Success();
    }

    public async Task<IEnumerable<ReversalRequestDto>> GetReversalsForLoanAsync(int loanId)
    {
        var reversals = await _db.TransactionReversals
            .Include(r => r.RequestedBy)
            .Include(r => r.ApprovedBy)
            .Include(r => r.Loan)
            .Where(r => r.LoanId == loanId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reversals.Select(MapToDto);
    }

    public async Task<IEnumerable<ReversalRequestDto>> GetPendingReversalsAsync()
    {
        var reversals = await _db.TransactionReversals
            .Include(r => r.RequestedBy)
            .Include(r => r.ApprovedBy)
            .Include(r => r.Loan)
            .Where(r => r.Status == ReversalStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return reversals.Select(MapToDto);
    }

    private static ReversalRequestDto MapToDto(TransactionReversal r) => new()
    {
        Id = r.Id,
        LoanId = r.LoanId,
        LoanAccountNo = r.Loan?.LoanAccountNo ?? "",
        OriginalTransactionId = r.OriginalTransactionId,
        ReversalType = r.ReversalType,
        OriginalAmount = r.OriginalAmount,
        ReversedAmount = r.ReversedAmount,
        Reason = r.Reason,
        Status = r.Status,
        RequestedBy = r.RequestedBy?.FullName ?? "",
        ApprovedBy = r.ApprovedBy?.FullName,
        CreatedAt = r.CreatedAt,
        ApprovedAt = r.ApprovedAt
    };

    private async Task SaveChangesWithConcurrencyHandlingAsync(string message)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(message);
        }
    }
}
