using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────
public class CollectionActionDto
{
    public string ActionType { get; set; } = "";
    public DateOnly ActionDate { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string Outcome { get; set; } = "";
    public DateOnly? PromiseDate { get; set; }
    public decimal? PromiseAmount { get; set; }
    public string? Notes { get; set; }
}

public class CollectionActionItemDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string CollectorName { get; set; } = "";
    public string ActionType { get; set; } = "";
    public DateOnly ActionDate { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string Outcome { get; set; } = "";
    public DateOnly? PromiseDate { get; set; }
    public decimal? PromiseAmount { get; set; }
    public string? PromiseStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CollectionQueueItemDto
{
    public int LoanId { get; set; }
    public string LoanAccountNo { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public decimal OutstandingAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int Dpd { get; set; }
    public string Bucket { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? AssignedCollector { get; set; }
    public int? AssignmentId { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public DateOnly? LastPromiseDate { get; set; }
    public string? LastPromiseStatus { get; set; }
    public string? LastActionOutcome { get; set; }
    public int TotalActions { get; set; }
}

public class CollectionSummaryDto
{
    public int TotalOverdueLoans { get; set; }
    public int TotalAssigned { get; set; }
    public int TotalUnassigned { get; set; }
    public int ActionsToday { get; set; }
    public int PromisesPending { get; set; }
    public int PromisesBroken { get; set; }
    public decimal TotalOverdueAmount { get; set; }
}

public class AssignCollectorDto
{
    public int CollectorUserId { get; set; }
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface ICollectionService
{
    Task<IEnumerable<CollectionQueueItemDto>> GetCollectorQueueAsync(int collectorUserId);
    Task<IEnumerable<CollectionQueueItemDto>> GetAllCollectionQueueAsync();
    Task<Result> AssignCollectorAsync(int loanId, int collectorUserId, int assignedByUserId);
    Task<Result> AddCollectionActionAsync(int loanId, CollectionActionDto dto, int userId);
    Task<IEnumerable<CollectionActionItemDto>> GetCollectionHistoryAsync(int loanId);
    Task<int> CheckBrokenPromisesAsync(DateOnly businessDate);
    Task<int> ApplyEscalationRulesAsync(DateOnly businessDate);
    Task<CollectionSummaryDto> GetCollectionSummaryAsync();
    Task<Result> AddCollectionNoteAsync(int loanId, LoanFlow.Application.DTOs.Loan.AddCollectionNoteRequest req, int userId);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class CollectionService : ICollectionService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessClock _businessClock;

    public CollectionService(LoanFlowDbContext db, IBusinessClock businessClock)
    {
        _db = db;
        _businessClock = businessClock;
    }

    public async Task<IEnumerable<CollectionQueueItemDto>> GetCollectorQueueAsync(int collectorUserId)
    {
        var assignedLoanIds = await _db.CollectorAssignments
            .Where(a => a.CollectorUserId == collectorUserId && a.Status == CollectorAssignmentStatus.Active)
            .Select(a => a.LoanId)
            .ToListAsync();

        return await BuildQueueAsync(l => assignedLoanIds.Contains(l.Id));
    }

    public async Task<IEnumerable<CollectionQueueItemDto>> GetAllCollectionQueueAsync()
    {
        return await BuildQueueAsync(l => l.Status == LoanStatus.Overdue);
    }

    private async Task<IEnumerable<CollectionQueueItemDto>> BuildQueueAsync(
        System.Linq.Expressions.Expression<Func<Loan, bool>> filter)
    {
        var today = _businessClock.TodayBusinessDate();

        var loans = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.RepaymentSchedules)
            .Where(filter)
            .ToListAsync();

        var loanIds = loans.Select(l => l.Id).ToList();

        var assignments = await _db.CollectorAssignments
            .Include(a => a.Collector)
            .Where(a => loanIds.Contains(a.LoanId) && a.Status == CollectorAssignmentStatus.Active)
            .ToDictionaryAsync(a => a.LoanId);

        var lastActions = await _db.CollectionActions
            .Where(a => loanIds.Contains(a.LoanId))
            .GroupBy(a => a.LoanId)
            .Select(g => new
            {
                LoanId = g.Key,
                Count = g.Count(),
                Last = g.OrderByDescending(a => a.CreatedAt).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.LoanId);

        var result = new List<CollectionQueueItemDto>();

        foreach (var loan in loans)
        {
            var overdueSchedules = loan.RepaymentSchedules
                .Where(s => s.DueDate < today && s.RemainingDueAmount > 0)
                .ToList();

            if (overdueSchedules.Count == 0 && loan.Status != LoanStatus.Overdue) continue;

            var oldestDue = overdueSchedules.Count > 0 ? overdueSchedules.Min(s => s.DueDate) : today;
            int dpd = today.DayNumber - oldestDue.DayNumber;
            var bucket = DpdBucket.Calculate(dpd);

            assignments.TryGetValue(loan.Id, out var assignment);
            lastActions.TryGetValue(loan.Id, out var actionInfo);

            result.Add(new CollectionQueueItemDto
            {
                LoanId = loan.Id,
                LoanAccountNo = loan.LoanAccountNo,
                CustomerId = loan.CustomerId,
                CustomerName = loan.Customer.FullName,
                CustomerPhone = loan.Customer.Phone,
                OutstandingAmount = loan.OutstandingAmount,
                OverdueAmount = overdueSchedules.Sum(s => s.RemainingDueAmount),
                Dpd = dpd,
                Bucket = bucket,
                Priority = CollectionPriority.FromBucket(bucket),
                AssignedCollector = assignment?.Collector?.FullName,
                AssignmentId = assignment?.Id,
                NextFollowUpDate = actionInfo?.Last?.NextFollowUpDate,
                LastPromiseDate = actionInfo?.Last?.PromiseDate,
                LastPromiseStatus = actionInfo?.Last?.PromiseStatus,
                LastActionOutcome = actionInfo?.Last?.Outcome,
                TotalActions = actionInfo?.Count ?? 0
            });
        }

        return result.OrderByDescending(x => x.Dpd);
    }

    public async Task<Result> AssignCollectorAsync(int loanId, int collectorUserId, int assignedByUserId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure("Loan not found.");

        var collector = await _db.Users.FindAsync(collectorUserId);
        if (collector is null || collector.Role != Roles.Collector)
            return Result.Failure("User is not a valid collector.");

        // Deactivate any existing assignment
        var existing = await _db.CollectorAssignments
            .FirstOrDefaultAsync(a => a.LoanId == loanId && a.Status == CollectorAssignmentStatus.Active);

        if (existing != null)
        {
            existing.Status = CollectorAssignmentStatus.Transferred;
            existing.UnassignedAt = _businessClock.UtcNow;
        }

        // Compute priority from current DPD
        var today = _businessClock.TodayBusinessDate();
        var overdueSchedules = await _db.RepaymentSchedules
            .Where(s => s.LoanId == loanId && s.DueDate < today && s.RemainingDueAmount > 0)
            .ToListAsync();
        var dpd = overdueSchedules.Count > 0 ? today.DayNumber - overdueSchedules.Min(s => s.DueDate).DayNumber : 0;

        _db.CollectorAssignments.Add(new CollectorAssignment
        {
            LoanId = loanId,
            CollectorUserId = collectorUserId,
            Priority = CollectionPriority.FromBucket(DpdBucket.Calculate(dpd))
        });

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = assignedByUserId,
            ActionCode = AuditActions.CollectorAssigned,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = AuditHelper.ToJson(new { collectorUserId, collectorName = collector.FullName })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> AddCollectionActionAsync(int loanId, CollectionActionDto dto, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure("Loan not found.");

        if (!CollectionActionType.All.Contains(dto.ActionType))
            return Result.Failure($"Invalid action type: {dto.ActionType}");

        if (!CollectionOutcome.All.Contains(dto.Outcome))
            return Result.Failure($"Invalid outcome: {dto.Outcome}");

        var action = new CollectionAction
        {
            LoanId = loanId,
            CollectorUserId = userId,
            ActionType = dto.ActionType,
            ActionDate = dto.ActionDate,
            NextFollowUpDate = dto.NextFollowUpDate,
            Outcome = dto.Outcome,
            Notes = dto.Notes
        };

        // Handle promise-to-pay
        if (dto.Outcome == CollectionOutcome.PromiseToPay)
        {
            if (!dto.PromiseDate.HasValue || !dto.PromiseAmount.HasValue)
                return Result.Failure("Promise date and amount are required for PromiseToPay outcome.");

            action.PromiseDate = dto.PromiseDate;
            action.PromiseAmount = dto.PromiseAmount;
            action.PromiseStatus = Domain.Constants.PromiseStatus.Pending;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.PromiseRecorded,
                EntityType = "Loan",
                EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new { promiseDate = dto.PromiseDate, promiseAmount = dto.PromiseAmount })
            });
        }

        _db.CollectionActions.Add(action);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.CollectionActionAdded,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = AuditHelper.ToJson(new { dto.ActionType, dto.Outcome })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<IEnumerable<CollectionActionItemDto>> GetCollectionHistoryAsync(int loanId)
    {
        return await _db.CollectionActions
            .Include(a => a.Collector)
            .Where(a => a.LoanId == loanId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new CollectionActionItemDto
            {
                Id = a.Id,
                LoanId = a.LoanId,
                CollectorName = a.Collector.FullName,
                ActionType = a.ActionType,
                ActionDate = a.ActionDate,
                NextFollowUpDate = a.NextFollowUpDate,
                Outcome = a.Outcome,
                PromiseDate = a.PromiseDate,
                PromiseAmount = a.PromiseAmount,
                PromiseStatus = a.PromiseStatus,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<Result> AddCollectionNoteAsync(int loanId, LoanFlow.Application.DTOs.Loan.AddCollectionNoteRequest req, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure("Loan not found.");

        if (string.IsNullOrWhiteSpace(req.NoteText))
            return Result.Failure("COL-VAL-001: Collection note cannot be empty.");

        _db.CollectionNotes.Add(new CollectionNote
        {
            LoanId = loanId,
            NoteDate = req.NoteDate,
            NoteText = req.NoteText,
            CreatedByUserId = userId
        });

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.CollectionNoteAdded,
            EntityType = "Loan",
            EntityId = loanId
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<int> CheckBrokenPromisesAsync(DateOnly businessDate)
    {
        // Find all pending promises where PromiseDate < today
        var brokenActions = await _db.CollectionActions
            .Where(a => a.PromiseStatus == Domain.Constants.PromiseStatus.Pending
                     && a.PromiseDate.HasValue
                     && a.PromiseDate.Value < businessDate)
            .ToListAsync();

        if (brokenActions.Count == 0) return 0;

        // Fetch all positive payments for the affected loans
        var loanIds = brokenActions.Select(a => a.LoanId).Distinct().ToList();
        var payments = await _db.Payments
            .Where(p => loanIds.Contains(p.LoanId) && p.Amount > 0)
            .Select(p => new { p.LoanId, p.PaymentDate, p.Amount })
            .ToListAsync();
            
        var paymentsByLoan = payments.GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var action in brokenActions)
        {
            var promisedAmount = action.PromiseAmount ?? 0m;
            var promiseDate = action.PromiseDate!.Value;
            var paidTowardPromise = paymentsByLoan.TryGetValue(action.LoanId, out var loanPayments)
                ? loanPayments
                    .Where(p => p.PaymentDate >= action.ActionDate && p.PaymentDate <= promiseDate)
                    .Sum(p => p.Amount)
                : 0m;

            // BA Rule (REL-002): Promise is Kept if paid >= 98% of promised amount
            // Allows 2% tolerance for bank rounding/fee discrepancy
            const decimal keptThreshold = 0.98m;
            if (paidTowardPromise >= promisedAmount * keptThreshold && promisedAmount > 0)
            {
                action.PromiseStatus = Domain.Constants.PromiseStatus.Kept;
            }
            else
            {
                action.PromiseStatus = Domain.Constants.PromiseStatus.Broken;

                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = null, // system action
                    ActionCode = AuditActions.PromiseBroken,
                    EntityType = "Loan",
                    EntityId = action.LoanId,
                    NewValuesJson = AuditHelper.ToJson(new { promiseDate = action.PromiseDate, promiseAmount = action.PromiseAmount })
                });
            }
        }

        await _db.SaveChangesAsync();

        return brokenActions.Count(a => a.PromiseStatus == Domain.Constants.PromiseStatus.Broken);
    }

    public async Task<CollectionSummaryDto> GetCollectionSummaryAsync()
    {
        var today = _businessClock.TodayBusinessDate();

        var overdueLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Overdue);
        var totalAssigned = await _db.CollectorAssignments.CountAsync(a => a.Status == CollectorAssignmentStatus.Active);
        var actionsToday = await _db.CollectionActions.CountAsync(a => a.ActionDate == today);
        var promisesPending = await _db.CollectionActions.CountAsync(a => a.PromiseStatus == Domain.Constants.PromiseStatus.Pending);
        var promisesBroken = await _db.CollectionActions.CountAsync(a => a.PromiseStatus == Domain.Constants.PromiseStatus.Broken);
        var totalOverdueAmount = await _db.Loans.Where(l => l.Status == LoanStatus.Overdue).SumAsync(l => l.OutstandingAmount);

        return new CollectionSummaryDto
        {
            TotalOverdueLoans = overdueLoans,
            TotalAssigned = totalAssigned,
            TotalUnassigned = overdueLoans - totalAssigned,
            ActionsToday = actionsToday,
            PromisesPending = promisesPending,
            PromisesBroken = promisesBroken,
            TotalOverdueAmount = totalOverdueAmount
        };
    }

    public async Task<int> ApplyEscalationRulesAsync(DateOnly businessDate)
    {
        var rules = await _db.EscalationRules.Where(r => r.Status == "Active").ToListAsync();
        if (!rules.Any()) return 0;
        
        var overdueLoans = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Where(l => l.Status == LoanStatus.Overdue)
            .ToListAsync();
            
        int actionsTaken = 0;
        
        foreach (var loan in overdueLoans)
        {
            var earliestOverdue = loan.RepaymentSchedules
                .Where(s => s.RemainingDueAmount > 0)
                .OrderBy(s => s.DueDate)
                .FirstOrDefault();
                
            if (earliestOverdue == null) continue;
            
            int dpd = businessDate.DayNumber - earliestOverdue.DueDate.DayNumber;
            if (dpd <= 0) continue;
            
            // Re-evaluate bucket matching Escalation Rules
            foreach (var rule in rules)
            {
                bool matches = false;
                switch (rule.DpdCondition)
                {
                    case ">": matches = dpd > rule.DpdThreshold; break;
                    case ">=": matches = dpd >= rule.DpdThreshold; break;
                    case "==": matches = dpd == rule.DpdThreshold; break;
                    case "<": matches = dpd < rule.DpdThreshold; break;
                    case "<=": matches = dpd <= rule.DpdThreshold; break;
                }
                
                if (matches)
                {
                    // Prevent duplicate escalations of the exact same type on the same date
                    var alreadyEscalated = await _db.CollectionActions.AnyAsync(a => 
                        a.LoanId == loan.Id && 
                        a.ActionType == "SystemEscalation" && 
                        a.Notes!.Contains(rule.ActionToTake) &&
                        a.ActionDate == businessDate);
                        
                    if (!alreadyEscalated)
                    {
                        var ca = new CollectionAction
                        {
                            LoanId = loan.Id,
                            CollectorUserId = 0, // System
                            ActionType = "SystemEscalation",
                            ActionDate = businessDate,
                            Outcome = "Escalated",
                            Notes = $"Auto-Escalated due to rule: {dpd} DPD {rule.DpdCondition} {rule.DpdThreshold}. Target Action: {rule.ActionToTake}. Assigned bucket: {rule.Bucket}",
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.CollectionActions.Add(ca);
                        actionsTaken++;
                    }
                }
            }
        }
        
        if (actionsTaken > 0)
        {
            await _db.SaveChangesAsync();
        }
        
        return actionsTaken;
    }
}
