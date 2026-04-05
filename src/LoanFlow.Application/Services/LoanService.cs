using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILoanService
{
    Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter);
    Task<Result<LoanDto>> GetByIdAsync(int id);
    Task<Result<LoanDto>> DisburseAsync(CreateLoanRequest request, int userId);
    Task<Result> CollectPaymentAsync(int loanId, CollectPaymentRequest request, int userId);
    Task<IEnumerable<RepaymentScheduleDto>> GetScheduleAsync(int loanId);
}

public class LoanService : ILoanService
{
    private readonly LoanFlowDbContext _db;

    public LoanService(LoanFlowDbContext db) => _db = db;

    public async Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter)
    {
        var query = _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanProduct)
            .Include(l => l.LoanApplication)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(l => l.LoanNumber.Contains(s) || l.Customer.FullName.ToLower().Contains(s));
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
            Items = items.Select(MapToDto),
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<Result<LoanDto>> GetByIdAsync(int id)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer).Include(l => l.LoanProduct)
            .Include(l => l.LoanApplication)
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.Payments).ThenInclude(p => p.CollectedBy)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (loan is null) return Result.Failure<LoanDto>("Khoản vay không tồn tại.");
        var dto = MapToDto(loan);
        dto.Schedule = loan.RepaymentSchedules.OrderBy(s => s.InstallmentNumber).Select(MapSchedule).ToList();
        dto.RecentPayments = loan.Payments.OrderByDescending(p => p.PaymentDate).Take(10).Select(MapPayment).ToList();
        return Result.Success(dto);
    }

    public async Task<Result<LoanDto>> DisburseAsync(CreateLoanRequest req, int userId)
    {
        var application = await _db.LoanApplications
            .Include(a => a.LoanProduct)
            .FirstOrDefaultAsync(a => a.Id == req.LoanApplicationId);

        if (application is null) return Result.Failure<LoanDto>("Hồ sơ không tồn tại.");
        if (application.Status != ApplicationStatus.Approved)
            return Result.Failure<LoanDto>("Chỉ giải ngân được hồ sơ đã phê duyệt.");
        if (await _db.Loans.AnyAsync(l => l.LoanApplicationId == req.LoanApplicationId))
            return Result.Failure<LoanDto>("Hồ sơ này đã được giải ngân.");

        var product = application.LoanProduct;
        var r = product.InterestRatePerMonth / 100m;
        var n = application.TenorMonths;
        var pow = (decimal)Math.Pow((double)(1 + r), n);
        var monthly = application.RequestedAmount * r * pow / (pow - 1);

        var loanNumber = await GenerateLoanNumberAsync();
        var loan = new Loan
        {
            LoanNumber = loanNumber,
            LoanApplicationId = req.LoanApplicationId,
            CustomerId = application.CustomerId,
            LoanProductId = application.LoanProductId,
            PrincipalAmount = application.RequestedAmount,
            InterestRatePerMonth = product.InterestRatePerMonth,
            TenorMonths = application.TenorMonths,
            MonthlyInstallment = Math.Round(monthly, 0),
            OutstandingBalance = application.RequestedAmount,
            DisbursedAt = req.DisbursedAt,
            MaturityDate = req.DisbursedAt.AddMonths(application.TenorMonths),
            Status = LoanStatus.Active,
            CreatedByUserId = userId
        };
        _db.Loans.Add(loan);
        await _db.SaveChangesAsync();

        // Disbursement record
        _db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = loan.Id,
            Amount = application.RequestedAmount,
            DisbursedAt = req.DisbursedAt,
            PaymentMethod = req.PaymentMethod,
            ReferenceNumber = req.ReferenceNumber,
            Notes = req.Notes,
            ProcessedByUserId = userId
        });

        // Generate repayment schedule
        var balance = application.RequestedAmount;
        for (int i = 1; i <= n; i++)
        {
            var interest = Math.Round(balance * r, 0);
            var principal = Math.Round(Math.Min(monthly - interest, balance), 0);
            balance -= principal;

            _db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNumber = i,
                DueDate = req.DisbursedAt.AddMonths(i),
                PrincipalAmount = principal,
                InterestAmount = interest,
                TotalDue = principal + interest,
                RemainingBalance = balance
            });
        }

        // Mark application as disbursed (update status)
        application.Status = "Disbursed";
        await _db.SaveChangesAsync();

        return await GetByIdAsync(loan.Id);
    }

    public async Task<Result> CollectPaymentAsync(int loanId, CollectPaymentRequest req, int userId)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure("Khoản vay không tồn tại.");
        if (loan.Status != LoanStatus.Active) return Result.Failure("Khoản vay không ở trạng thái hoạt động.");

        // Waterfall allocation: fees → penalty → interest → principal
        var remaining = req.Amount;
        var totalInterestPaid = 0m;
        var totalPrincipalPaid = 0m;

        var pendingSchedules = loan.RepaymentSchedules
            .Where(s => s.Status != ScheduleStatus.Paid)
            .OrderBy(s => s.InstallmentNumber)
            .ToList();

        foreach (var schedule in pendingSchedules)
        {
            if (remaining <= 0) break;
            var due = schedule.TotalDue - schedule.PaidAmount;
            var pay = Math.Min(remaining, due);

            // Split proportionally
            var interestPay = Math.Min(pay, schedule.InterestAmount);
            var principalPay = pay - interestPay;

            schedule.PaidAmount += pay;
            schedule.Status = schedule.PaidAmount >= schedule.TotalDue
                ? ScheduleStatus.Paid
                : ScheduleStatus.PartiallyPaid;
            schedule.PaidAt = schedule.Status == ScheduleStatus.Paid ? req.PaymentDate : null;

            totalInterestPaid += interestPay;
            totalPrincipalPaid += principalPay;
            remaining -= pay;
        }

        loan.TotalPaid += req.Amount - remaining;
        loan.OutstandingBalance -= totalPrincipalPaid;
        if (loan.OutstandingBalance <= 0)
        {
            loan.OutstandingBalance = 0;
            loan.Status = LoanStatus.Closed;
        }

        _db.Payments.Add(new Payment
        {
            LoanId = loanId,
            Amount = req.Amount - remaining,
            PrincipalPaid = totalPrincipalPaid,
            InterestPaid = totalInterestPaid,
            PaymentDate = req.PaymentDate,
            PaymentMethod = req.PaymentMethod,
            ReferenceNumber = req.ReferenceNumber,
            Notes = req.Notes,
            CollectedByUserId = userId
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<IEnumerable<RepaymentScheduleDto>> GetScheduleAsync(int loanId)
    {
        return await _db.RepaymentSchedules
            .Where(s => s.LoanId == loanId)
            .OrderBy(s => s.InstallmentNumber)
            .Select(s => MapSchedule(s))
            .ToListAsync();
    }

    private async Task<string> GenerateLoanNumberAsync()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"LN-{today}-";
        var last = await _db.Loans
            .Where(l => l.LoanNumber.StartsWith(prefix))
            .OrderByDescending(l => l.LoanNumber)
            .Select(l => l.LoanNumber)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (last is not null)
        {
            var parts = last.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var n)) seq = n + 1;
        }
        return $"{prefix}{seq:D4}";
    }

    private static LoanDto MapToDto(Loan l) => new()
    {
        Id = l.Id, LoanNumber = l.LoanNumber,
        ApplicationNumber = l.LoanApplication?.ApplicationNumber ?? "",
        CustomerId = l.CustomerId, CustomerName = l.Customer?.FullName ?? "",
        ProductName = l.LoanProduct?.Name ?? "",
        PrincipalAmount = l.PrincipalAmount, InterestRatePerMonth = l.InterestRatePerMonth,
        TenorMonths = l.TenorMonths, MonthlyInstallment = l.MonthlyInstallment,
        OutstandingBalance = l.OutstandingBalance, TotalPaid = l.TotalPaid,
        Status = l.Status, DisbursedAt = l.DisbursedAt, MaturityDate = l.MaturityDate
    };

    private static RepaymentScheduleDto MapSchedule(RepaymentSchedule s) => new()
    {
        InstallmentNumber = s.InstallmentNumber, DueDate = s.DueDate,
        PrincipalAmount = s.PrincipalAmount, InterestAmount = s.InterestAmount,
        TotalDue = s.TotalDue, PaidAmount = s.PaidAmount,
        RemainingBalance = s.RemainingBalance, Status = s.Status, PaidAt = s.PaidAt
    };

    private static PaymentSummaryDto MapPayment(Payment p) => new()
    {
        Id = p.Id, Amount = p.Amount, PrincipalPaid = p.PrincipalPaid,
        InterestPaid = p.InterestPaid, PaymentDate = p.PaymentDate,
        PaymentMethod = p.PaymentMethod, CollectedBy = p.CollectedBy?.FullName ?? ""
    };
}
