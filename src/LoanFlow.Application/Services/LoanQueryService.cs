using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── Interface ────────────────────────────────────────────────────────────────

public interface ILoanQueryService
{
    Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter);
    Task<Result<LoanDto>>      GetByIdAsync(int id);
    Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId);
    Task<IEnumerable<PaymentDto>>      GetPaymentsAsync(int loanId);
    Task<IEnumerable<OverdueItemDto>>  GetOverdueListAsync();
}

// ── Implementation ───────────────────────────────────────────────────────────

/// <summary>
/// Read-only queries cho Loan (không mutate state).
/// Tách ra từ LoanService để giảm từ 56KB → ~15KB mỗi service.
/// </summary>
public class LoanQueryService : ILoanQueryService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessClock    _businessClock;

    public LoanQueryService(LoanFlowDbContext db, IBusinessClock businessClock)
    {
        _db            = db;
        _businessClock = businessClock;
    }

    // ── GET /loans ────────────────────────────────────────────────────────────

    public async Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter)
    {
        var query = _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.Application)
            .Include(l => l.Disbursement)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(l => l.LoanAccountNo.Contains(s) || l.Customer.FullName.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(l => l.Status == filter.Status);
        if (filter.CustomerId.HasValue)
            query = query.Where(l => l.CustomerId == filter.CustomerId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<LoanDto>
        {
            Items      = items.Select(LoanMappers.MapToDto),
            TotalCount = total,
            Page       = filter.Page,
            PageSize   = filter.PageSize
        };
    }

    // ── GET /loans/{id} ───────────────────────────────────────────────────────

    public async Task<Result<LoanDto>> GetByIdAsync(int id)
    {
        var today = _businessClock.TodayBusinessDate();
        var loan  = await LoanMappers.LoadFullLoanAsync(_db, id);
        if (loan is null) return Result.Failure<LoanDto>("Loan not found.");

        var dto = LoanMappers.MapToDto(loan);

        dto.Schedule = loan.RepaymentSchedules
            .OrderBy(s => s.InstallmentNo)
            .Select(s =>
            {
                var item = LoanMappers.MapSchedule(s, today);
                item.UnpaidLateFeeAmount = loan.LateFees
                    .Where(f => f.ScheduleId == s.Id && f.Status == LateFeeStatus.Pending)
                    .Sum(f => f.FeeAmount);
                return item;
            })
            .ToList();

        dto.LateFees = loan.LateFees
            .Select(f => new LateFeeDto
            {
                Id = f.Id, ScheduleId = f.ScheduleId, OverdueAmount = f.OverdueAmount,
                DaysOverdue = f.DaysOverdue, FeeRate = f.FeeRate, FeeAmount = f.FeeAmount,
                Status = f.Status, CalculatedAt = f.CalculatedAt
            })
            .ToList();

        dto.Payments = loan.Payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(LoanMappers.MapPayment)
            .ToList();

        return Result.Success(dto);
    }

    // ── GET /loans/{id}/schedule ──────────────────────────────────────────────

    public async Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId)
    {
        var today = _businessClock.TodayBusinessDate();
        return await _db.RepaymentSchedules
            .Where(s => s.LoanId == loanId)
            .OrderBy(s => s.InstallmentNo)
            .Select(s => LoanMappers.MapSchedule(s, today))
            .ToListAsync();
    }

    // ── GET /loans/{id}/payments ──────────────────────────────────────────────

    public async Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int loanId)
        => await _db.Payments
            .Include(p => p.CreatedBy)
            .Where(p => p.LoanId == loanId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => LoanMappers.MapPayment(p))
            .ToListAsync();

    // ── GET /overdue ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync()
    {
        var today = _businessClock.TodayBusinessDate();

        var overdueData = await _db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .Where(l => l.RepaymentSchedules.Any(s => s.DueDate < today && s.RemainingDueAmount > 0))
            .Select(l => new OverdueItemDto
            {
                LoanId          = l.Id,
                LoanAccountNo   = l.LoanAccountNo,
                CustomerId      = l.CustomerId,
                CustomerName    = l.Customer.FullName,
                CustomerPhone   = l.Customer.Phone,
                OutstandingAmount = l.OutstandingAmount,
                OverdueDueAmount  = l.RepaymentSchedules
                    .Where(s => s.DueDate < today && s.RemainingDueAmount > 0)
                    .Sum(s => s.RemainingDueAmount),
                Dpd = today.DayNumber - l.RepaymentSchedules
                    .Where(s => s.DueDate < today && s.RemainingDueAmount > 0)
                    .Min(s => s.DueDate).DayNumber,
                LastCollectionNote = l.CollectionNotes
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => n.NoteText)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.Dpd)
            .ToListAsync();

        foreach (var item in overdueData)
            item.Bucket = DpdBucket.Calculate(item.Dpd);

        return overdueData;
    }
}
