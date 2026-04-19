using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanAdjustment;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILoanAdjustmentService
{
    Task<Result> ApplyApprovedAdjustmentAsync(int approvalRequestId, int checkerUserId);
}

public class LoanAdjustmentService : ILoanAdjustmentService
{
    private readonly LoanFlowDbContext _db;

    public LoanAdjustmentService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Result> ApplyApprovedAdjustmentAsync(int approvalRequestId, int checkerUserId)
    {
        var request = await _db.PendingApprovalRequests.FindAsync(approvalRequestId);
        if (request == null) return Result.Failure("Approval request not found.");
        
        if (request.ActionType != "ManualAdjustment" || request.Status != "Approved")
            return Result.Failure("Invalid request type or status for loan adjustment.");

        LoanAdjustmentPayloadDto payload;
        try
        {
            payload = JsonSerializer.Deserialize<LoanAdjustmentPayloadDto>(request.Payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch
        {
            return Result.Failure("Invalid payload format.");
        }

        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == payload.LoanId);

        if (loan == null) return Result.Failure("Loan not found.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var adjustment = new LoanAdjustment
            {
                LoanId = payload.LoanId,
                AdjustmentType = payload.AdjustmentType,
                Amount = payload.Amount,
                ReasonCode = payload.ReasonCode,
                Note = payload.Note,
                CreatedByUserId = request.RequestedByUserId,
                ApprovedByUserId = checkerUserId,
                AppliedAt = DateTime.UtcNow
            };

            _db.LoanAdjustments.Add(adjustment);

            // Apply logic to Loan Outstanding Amount
            loan.OutstandingAmount += payload.Amount;
            
            // In a real banking system, adjusting the balance means adjusting specific schedules or keeping it in an accrued balance.
            // For MVP manual adjustment, we will just adjust the latest unpaid schedule.
            var schedule = loan.RepaymentSchedules
                .Where(s => s.Status != Domain.Constants.InstallmentStatus.Paid)
                .OrderBy(s => s.InstallmentNo)
                .FirstOrDefault();

            if (schedule != null)
            {
                if (payload.AdjustmentType == "Principal")
                    schedule.PrincipalDue += payload.Amount;
                else if (payload.AdjustmentType == "Interest")
                    schedule.InterestDue += payload.Amount;

                schedule.RemainingDueAmount += payload.Amount;
            }

            _db.AuditLogs.Add(new Domain.Entities.AuditLog
            {
                ActorUserId = checkerUserId,
                ActionCode = "LoanAdjustmentApplied",
                EntityType = "Loan",
                EntityId = loan.Id,
                NewValuesJson = AuditHelper.ToJson(new { 
                    adjustmentId = adjustment.Id, 
                    amount = payload.Amount, 
                    type = payload.AdjustmentType 
                })
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Result.Success();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
