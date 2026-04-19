using System;
using System.Linq;
using System.Threading.Tasks;
using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────

public class RestructureRequest
{
    public int NewTenorMonths { get; set; }
    public decimal NewMonthlyInterestRate { get; set; }
    public DateOnly RestructureStartDate { get; set; }
    public string ReasonCode { get; set; } = default!;  // "CustomerHardship" | "BusinessFailure"
    public string? Note { get; set; }
}

public class RescheduleRequest
{
    public int NewPaymentDayOfMonth { get; set; }       // 1 - 28
    public DateOnly EffectiveFrom { get; set; }
    public string ReasonCode { get; set; } = default!;
    public string? Note { get; set; }
}

public class WriteOffRequest
{
    public string ReasonCode { get; set; } = default!;  // "Uncollectible" | "BankruptCustomer" | "CourtOrder"
    public DateOnly WriteOffDate { get; set; }
    public string? Note { get; set; }
}

public class RecordRecoveryRequest
{
    public decimal RecoveredAmount { get; set; }
    public DateOnly RecoveryDate { get; set; }
    public string RecoveryMethod { get; set; } = default!; // "CashPayment" | "CollateralLiquidation" | "ThirdPartyCollection"
    public string? Note { get; set; }
}

// ── Interface ────────────────────────────────────────────────────────────────

public interface ILoanLifecycleService
{
    Task<Result<LoanRestructure>> RestructureAsync(int loanId, RestructureRequest request, int userId);
    Task<Result<LoanReschedule>> RescheduleAsync(int loanId, RescheduleRequest request, int userId);
    Task<Result<LoanWriteOff>> WriteOffAsync(int loanId, WriteOffRequest request, int userId);
    Task<Result<LoanRecovery>> RecordRecoveryAsync(int loanId, RecordRecoveryRequest request, int userId);
    Task<Result<CustomerExposureDto>> GetCustomerExposureAsync(int customerId);
}

public class CustomerExposureDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public int ActiveLoans { get; set; }
    public int RestructuredLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public bool ExceedsLimit { get; set; }
    public decimal MaxAllowedExposure { get; set; }
}

// ── Service Implementation ────────────────────────────────────────────────────

public class LoanLifecycleService : ILoanLifecycleService
{
    private readonly LoanFlowDbContext _db;
    private readonly IBusinessClock _clock;

    // Giới hạn exposure tối đa mỗi khách hàng = 500 triệu VNĐ (configurable)
    private const decimal DefaultMaxExposure = 500_000_000m;

    public LoanLifecycleService(LoanFlowDbContext db, IBusinessClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // ── BUP-201: Restructure ─────────────────────────────────────────────────
    public async Task<Result<LoanRestructure>> RestructureAsync(int loanId, RestructureRequest req, int userId)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure<LoanRestructure>("Loan not found.");

        // Chỉ restructure khi loan đang Active hoặc Overdue
        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Overdue)
            return Result.Failure<LoanRestructure>($"Cannot restructure a loan with status '{loan.Status}'.");

        if (req.NewTenorMonths <= 0)
            return Result.Failure<LoanRestructure>("New tenor must be greater than 0.");

        if (req.NewMonthlyInterestRate < 0)
            return Result.Failure<LoanRestructure>("New interest rate cannot be negative.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Lưu lại lịch sử cơ cấu
            var record = new LoanRestructure
            {
                LoanId = loanId,
                OldTenorMonths = loan.TenorMonths,
                OldMonthlyInterestRate = loan.MonthlyInterestRate,
                OldMaturityDate = loan.MaturityDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                NewTenorMonths = req.NewTenorMonths,
                NewMonthlyInterestRate = req.NewMonthlyInterestRate,
                NewMaturityDate = req.RestructureStartDate.AddMonths(req.NewTenorMonths),
                RestructureStartDate = req.RestructureStartDate,
                ReasonCode = req.ReasonCode,
                Note = req.Note,
                RequestedByUserId = userId,
                ApprovedByUserId = userId,  // Giả định: cùng người (đã qua Maker-Checker trước đó)
                RestructuredAt = DateTime.UtcNow
            };
            _db.LoanRestructures.Add(record);

            // Cập nhật Loan
            loan.TenorMonths = req.NewTenorMonths;
            loan.MonthlyInterestRate = req.NewMonthlyInterestRate;
            loan.MaturityDate = record.NewMaturityDate;
            loan.Status = LoanStatus.Restructured;
            loan.UpdatedAt = _clock.UtcNow;

            // Xóa toàn bộ schedule cũ chưa trả, tạo lại schedule mới
            var unpaidSchedules = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .ToList();
            _db.RepaymentSchedules.RemoveRange(unpaidSchedules);

            // Tính outstanding còn lại (chỉ principal)
            var remainingPrincipal = unpaidSchedules.Sum(s => s.PrincipalDue - s.PrincipalPaid);
            var n = req.NewTenorMonths;
            var rate = req.NewMonthlyInterestRate;

            // Tạo schedule mới theo phương pháp Flat
            var principalPerMonth = Math.Round(remainingPrincipal / n, 2);
            var lastPrincipal = remainingPrincipal - principalPerMonth * (n - 1);
            var interestPerMonth = Math.Round(remainingPrincipal * rate, 2);

            for (int i = 1; i <= n; i++)
            {
                var p = i == n ? lastPrincipal : principalPerMonth;
                _db.RepaymentSchedules.Add(new RepaymentSchedule
                {
                    LoanId = loanId,
                    InstallmentNo = i,
                    DueDate = req.RestructureStartDate.AddMonths(i),
                    PrincipalDue = p,
                    InterestDue = interestPerMonth,
                    PrincipalPaid = 0,
                    InterestPaid = 0,
                    RemainingDueAmount = p + interestPerMonth,
                    Status = InstallmentStatus.Unpaid
                });
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.LoanRestructured,
                EntityType = "Loan",
                EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new {
                    newTenor = req.NewTenorMonths,
                    newRate = req.NewMonthlyInterestRate,
                    reasonCode = req.ReasonCode
                })
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Result.Success(record);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── BUP-202: Reschedule ──────────────────────────────────────────────────
    public async Task<Result<LoanReschedule>> RescheduleAsync(int loanId, RescheduleRequest req, int userId)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure<LoanReschedule>("Loan not found.");

        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Overdue && loan.Status != LoanStatus.Restructured)
            return Result.Failure<LoanReschedule>($"Cannot reschedule loan with status '{loan.Status}'.");

        if (req.NewPaymentDayOfMonth < 1 || req.NewPaymentDayOfMonth > 28)
            return Result.Failure<LoanReschedule>("Payment day must be between 1 and 28.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Ghi lại DayOfMonth hiện tại từ schedule đầu tiên
            var firstUnpaid = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .OrderBy(s => s.InstallmentNo)
                .FirstOrDefault();

            int oldDay = firstUnpaid?.DueDate.Day ?? req.NewPaymentDayOfMonth;

            var record = new LoanReschedule
            {
                LoanId = loanId,
                OldPaymentDayOfMonth = oldDay,
                NewPaymentDayOfMonth = req.NewPaymentDayOfMonth,
                EffectiveFrom = req.EffectiveFrom,
                ReasonCode = req.ReasonCode,
                Note = req.Note,
                RequestedByUserId = userId,
                ApprovedByUserId = userId,
                RescheduledAt = DateTime.UtcNow
            };
            _db.LoanReschedules.Add(record);

            // Cập nhật DueDate của các installment từ EffectiveFrom trở đi
            var toUpdate = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid && s.DueDate >= req.EffectiveFrom)
                .ToList();

            foreach (var s in toUpdate)
            {
                // Giữ nguyên năm/tháng, đổi ngày
                s.DueDate = new DateOnly(s.DueDate.Year, s.DueDate.Month, req.NewPaymentDayOfMonth);
            }

            loan.UpdatedAt = _clock.UtcNow;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.LoanRescheduled,
                EntityType = "Loan",
                EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new {
                    oldDay,
                    newDay = req.NewPaymentDayOfMonth,
                    effectiveFrom = req.EffectiveFrom.ToString()
                })
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Result.Success(record);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── BUP-203: Write-Off ───────────────────────────────────────────────────
    public async Task<Result<LoanWriteOff>> WriteOffAsync(int loanId, WriteOffRequest req, int userId)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure<LoanWriteOff>("Loan not found.");

        if (loan.Status == LoanStatus.Closed || loan.Status == LoanStatus.WrittenOff)
            return Result.Failure<LoanWriteOff>($"Loan is already '{loan.Status}' and cannot be written off.");

        // Không cho write-off nếu đã có record
        if (await _db.LoanWriteOffs.AnyAsync(w => w.LoanId == loanId))
            return Result.Failure<LoanWriteOff>("A write-off record already exists for this loan.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var unpaidPrincipal = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.PrincipalDue - s.PrincipalPaid);

            var unpaidInterest = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid)
                .Sum(s => s.InterestDue - s.InterestPaid);

            var unpaidLateFees = loan.LateFees
                .Where(f => f.Status == LateFeeStatus.Pending)
                .Sum(f => f.FeeAmount);

            var writeOff = new LoanWriteOff
            {
                LoanId = loanId,
                WriteOffAmount = unpaidPrincipal + unpaidInterest + unpaidLateFees,
                UnpaidPrincipal = unpaidPrincipal,
                UnpaidInterest = unpaidInterest,
                UnpaidLateFees = unpaidLateFees,
                ReasonCode = req.ReasonCode,
                Note = req.Note,
                WriteOffDate = req.WriteOffDate,
                RequestedByUserId = userId,
                ApprovedByUserId = userId,
                WrittenOffAt = DateTime.UtcNow
            };
            _db.LoanWriteOffs.Add(writeOff);

            // Cập nhật loan status
            loan.Status = LoanStatus.WrittenOff;
            loan.OutstandingAmount = 0;
            loan.UpdatedAt = _clock.UtcNow;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionCode = AuditActions.LoanWrittenOff,
                EntityType = "Loan",
                EntityId = loanId,
                NewValuesJson = AuditHelper.ToJson(new {
                    writeOffAmount = writeOff.WriteOffAmount,
                    reasonCode = req.ReasonCode
                })
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Result.Success(writeOff);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── BUP-203: Recovery ────────────────────────────────────────────────────
    public async Task<Result<LoanRecovery>> RecordRecoveryAsync(int loanId, RecordRecoveryRequest req, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure<LoanRecovery>("Loan not found.");

        if (loan.Status != LoanStatus.WrittenOff)
            return Result.Failure<LoanRecovery>("Recovery can only be recorded for written-off loans.");

        var writeOff = await _db.LoanWriteOffs.FirstOrDefaultAsync(w => w.LoanId == loanId);
        if (writeOff is null) return Result.Failure<LoanRecovery>("Write-off record not found.");

        if (req.RecoveredAmount <= 0)
            return Result.Failure<LoanRecovery>("Recovered amount must be greater than 0.");

        // Tính tổng đã thu hồi
        var totalRecovered = await _db.LoanRecoveries
            .Where(r => r.LoanId == loanId)
            .SumAsync(r => r.RecoveredAmount);

        if (totalRecovered + req.RecoveredAmount > writeOff.WriteOffAmount)
            return Result.Failure<LoanRecovery>($"Total recovery ({totalRecovered + req.RecoveredAmount:N0}) would exceed write-off amount ({writeOff.WriteOffAmount:N0}).");

        var recovery = new LoanRecovery
        {
            LoanId = loanId,
            WriteOffId = writeOff.Id,
            RecoveredAmount = req.RecoveredAmount,
            RecoveryDate = req.RecoveryDate,
            RecoveryMethod = req.RecoveryMethod,
            Note = req.Note,
            RecordedByUserId = userId,
            RecordedAt = DateTime.UtcNow
        };
        _db.LoanRecoveries.Add(recovery);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.LoanRecoveryRecorded,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = AuditHelper.ToJson(new {
                recoveredAmount = req.RecoveredAmount,
                method = req.RecoveryMethod
            })
        });

        await _db.SaveChangesAsync();

        return Result.Success(recovery);
    }

    // ── BUP-204: Customer Exposure ───────────────────────────────────────────
    public async Task<Result<CustomerExposureDto>> GetCustomerExposureAsync(int customerId)
    {
        var customer = await _db.Customers
            .Include(c => c.Loans)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer is null) return Result.Failure<CustomerExposureDto>("Customer not found.");

        var activeLoans = customer.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToList();

        var totalOutstanding = activeLoans.Sum(l => l.OutstandingAmount);
        var totalApproved = activeLoans.Sum(l => l.ApprovedAmount);

        return Result.Success(new CustomerExposureDto
        {
            CustomerId = customerId,
            CustomerName = customer.FullName,
            ActiveLoans = activeLoans.Count(l => l.Status == LoanStatus.Active),
            RestructuredLoans = activeLoans.Count(l => l.Status == LoanStatus.Restructured),
            TotalOutstanding = totalOutstanding,
            TotalApprovedAmount = totalApproved,
            MaxAllowedExposure = DefaultMaxExposure,
            ExceedsLimit = totalOutstanding > DefaultMaxExposure
        });
    }
}
