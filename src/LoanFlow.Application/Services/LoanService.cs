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
    Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId);
    Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest request, int userId);
    Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest request, int userId);
    Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId);
    Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int loanId);
    Task<Result> AddCollectionNoteAsync(int loanId, AddCollectionNoteRequest request, int userId);
    Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync();
}

public class LoanService : ILoanService
{
    private readonly LoanFlowDbContext _db;

    public LoanService(LoanFlowDbContext db) => _db = db;

    // ── Timezone helper (§12.8 Business date = Asia/Ho_Chi_Minh) ────────────
    private static DateOnly BusinessDate()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // UTC+7
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    // ── GET /loans ──────────────────────────────────────────────────────────
    public async Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter)
    {
        var query = _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.Application)
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
            Items = items.Select(MapToDto),
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    // ── GET /loans/{id} ─────────────────────────────────────────────────────
    public async Task<Result<LoanDto>> GetByIdAsync(int id)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.Application)
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.Payments).ThenInclude(p => p.CreatedBy)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (loan is null) return Result.Failure<LoanDto>("Loan not found.");

        var dto = MapToDto(loan);
        var today = BusinessDate();

        dto.Schedule = loan.RepaymentSchedules
            .OrderBy(s => s.InstallmentNo)
            .Select(s => MapSchedule(s, today))
            .ToList();

        dto.Payments = loan.Payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(MapPayment)
            .ToList();

        return Result.Success(dto);
    }

    // ── POST /applications/{id}/create-loan (§LOAN-001) ────────────────────
    public async Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId)
    {
        var application = await _db.LoanApplications
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null) return Result.Failure<LoanDto>("LOAN-VAL-001: Application not found.");

        // §12.6 Rule 1: only approved application creates loan
        if (application.Status != ApplicationStatus.Approved)
            return Result.Failure<LoanDto>("LOAN-VAL-001: Only approved application can create loan.");

        // §12.6 Rule 1: one loan only
        if (await _db.Loans.AnyAsync(l => l.ApplicationId == applicationId))
            return Result.Failure<LoanDto>("LOAN-VAL-002: Loan already created for this application.");

        var product = application.Product;
        var loanNo = await GenerateLoanAccountNoAsync();

        var loan = new Loan
        {
            LoanAccountNo = loanNo,
            ApplicationId = applicationId,
            CustomerId = application.CustomerId,
            ProductId = application.ProductId,
            ApprovedAmount = application.RequestedAmount,
            TenorMonths = application.RequestedTenorMonths,
            MonthlyInterestRate = product.MonthlyInterestRate,     // snapshot
            ProcessingFee = product.ProcessingFee,                  // snapshot
            Status = LoanStatus.PendingDisbursement,
            OutstandingAmount = application.RequestedAmount
        };
        _db.Loans.Add(loan);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.LoanCreated,
            EntityType = "Loan",
            EntityId = loan.Id,
            NewValuesJson = $"{{\"loanAccountNo\":\"{loanNo}\",\"applicationId\":{applicationId}}}"
        });
        await _db.SaveChangesAsync();

        return await GetByIdAsync(loan.Id);
    }

    // ── POST /loans/{id}/disburse (§LOAN-002) ────────────────────────────────
    public async Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest req, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure<LoanDto>("Loan not found.");

        // DISB-VAL-001
        if (loan.Status != LoanStatus.PendingDisbursement)
            return Result.Failure<LoanDto>("DISB-VAL-001: Loan is not ready for disbursement.");

        // DISB-VAL-003
        if (await _db.LoanDisbursements.AnyAsync(d => d.LoanId == loanId))
            return Result.Failure<LoanDto>("DISB-VAL-003: Loan already disbursed.");

        // DISB-VAL-002: disbursed amount must equal approved amount (§12.5 Rule 3)
        if (req.DisbursedAmount != loan.ApprovedAmount)
            return Result.Failure<LoanDto>($"DISB-VAL-002: Disbursed amount must equal approved amount ({loan.ApprovedAmount:N0}).");

        // §12.5 Rule 4: loan becomes ACTIVE
        loan.DisbursementDate = req.DisbursementDate;
        loan.MaturityDate = req.DisbursementDate.AddMonths(loan.TenorMonths);
        loan.Status = LoanStatus.Active;
        loan.UpdatedAt = DateTime.UtcNow;

        _db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = loanId,
            DisbursedAmount = req.DisbursedAmount,
            DisbursementDate = req.DisbursementDate,
            RecordedByUserId = userId
        });

        _db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = loanId,
            TransactionType = "Disbursement",
            Amount = req.DisbursedAmount
        });

        // §12.5 Rule 5 + §13.2: Generate repayment schedule — FLAT model
        GenerateSchedule(loan, req.DisbursementDate);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.LoanDisbursed,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = $"{{\"disbursedAmount\":{req.DisbursedAmount},\"disbursementDate\":\"{req.DisbursementDate}\"}}"
        });

        await _db.SaveChangesAsync();
        return await GetByIdAsync(loanId);
    }

    // ── POST /loans/{id}/payments (§PAY-001) ─────────────────────────────────
    public async Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest req, int userId)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null) return Result.Failure("Loan not found.");

        // PAY-VAL-001: loan must be ACTIVE or OVERDUE
        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Overdue)
            return Result.Failure("PAY-VAL-001: Loan is not in payable status.");

        // PAY-VAL-002: amount > 0
        if (req.Amount <= 0)
            return Result.Failure("PAY-VAL-002: Payment amount must be greater than zero.");

        // PAY-VAL-003: amount <= total outstanding due
        var totalDue = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .Sum(s => s.RemainingDueAmount);

        if (req.Amount > totalDue)
            return Result.Failure($"PAY-VAL-003: Payment amount exceeds current outstanding due ({totalDue:N2}).");

        // §13.6 Payment allocation: oldest installment first, interest before principal
        var remaining = req.Amount;
        var unpaidSchedules = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .OrderBy(s => s.InstallmentNo)
            .ToList();

        foreach (var schedule in unpaidSchedules)
        {
            if (remaining <= 0) break;

            // Interest first
            var interestDue = schedule.InterestDue - schedule.InterestPaid;
            var interestPay = Math.Min(remaining, interestDue);
            schedule.InterestPaid += interestPay;
            remaining -= interestPay;

            // Then principal
            if (remaining > 0)
            {
                var principalDue = schedule.PrincipalDue - schedule.PrincipalPaid;
                var principalPay = Math.Min(remaining, principalDue);
                schedule.PrincipalPaid += principalPay;
                remaining -= principalPay;
            }

            // Update remaining due and status
            schedule.RemainingDueAmount = (schedule.PrincipalDue - schedule.PrincipalPaid)
                                        + (schedule.InterestDue - schedule.InterestPaid);

            schedule.Status = schedule.RemainingDueAmount <= 0
                ? InstallmentStatus.Paid
                : InstallmentStatus.Partial;
        }

        // §13.3 Update loan outstanding = sum of remaining due
        loan.OutstandingAmount = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid)
            .Sum(s => s.RemainingDueAmount);

        // §11.2: ACTIVE → CLOSED if all paid
        if (loan.RepaymentSchedules.All(s => s.Status == InstallmentStatus.Paid))
        {
            loan.Status = LoanStatus.Closed;
        }
        // §11.2: OVERDUE → ACTIVE if all overdue installments paid
        else if (loan.Status == LoanStatus.Overdue)
        {
            var today = BusinessDate();
            var hasOverdue = loan.RepaymentSchedules.Any(s =>
                s.Status != InstallmentStatus.Paid && s.DueDate < today);
            if (!hasOverdue) loan.Status = LoanStatus.Active;
        }

        loan.UpdatedAt = DateTime.UtcNow;

        var payment = new Payment
        {
            LoanId = loanId,
            PaymentDate = req.PaymentDate,
            Amount = req.Amount,
            PaymentMethod = req.PaymentMethod,
            Remarks = req.Remarks,
            CreatedByUserId = userId
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        _db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = loanId,
            PaymentId = payment.Id,
            TransactionType = "Payment",
            Amount = req.Amount
        });

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.PaymentRecorded,
            EntityType = "Payment",
            EntityId = payment.Id,
            NewValuesJson = $"{{\"amount\":{req.Amount},\"loanId\":{loanId}}}"
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    // ── GET /loans/{id}/schedule ─────────────────────────────────────────────
    public async Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId)
    {
        var today = BusinessDate();
        return await _db.RepaymentSchedules
            .Where(s => s.LoanId == loanId)
            .OrderBy(s => s.InstallmentNo)
            .Select(s => MapSchedule(s, today))
            .ToListAsync();
    }

    // ── GET /loans/{id}/payments ─────────────────────────────────────────────
    public async Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int loanId)
    {
        return await _db.Payments
            .Include(p => p.CreatedBy)
            .Where(p => p.LoanId == loanId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => MapPayment(p))
            .ToListAsync();
    }

    // ── POST /loans/{id}/collection-notes (§OD-003) ──────────────────────────
    public async Task<Result> AddCollectionNoteAsync(int loanId, AddCollectionNoteRequest req, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null) return Result.Failure("Loan not found.");

        // COL-VAL-001
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

    // ── GET /overdue (§OD-002) — query-time overdue calculation ─────────────
    public async Task<IEnumerable<OverdueItemDto>> GetOverdueListAsync()
    {
        var today = BusinessDate();

        var activeLoans = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.CollectionNotes)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .ToListAsync();

        var result = new List<OverdueItemDto>();

        foreach (var loan in activeLoans)
        {
            // §12.8 Rule 3: overdue if due_date < business_date AND remaining > 0
            var overdueSchedules = loan.RepaymentSchedules
                .Where(s => s.DueDate < today && s.RemainingDueAmount > 0)
                .ToList();

            if (overdueSchedules.Count == 0) continue;

            // §13.5 DPD = business_date - oldest overdue due_date
            var oldestDue = overdueSchedules.Min(s => s.DueDate);
            int dpd = today.DayNumber - oldestDue.DayNumber;
            var overdueAmount = overdueSchedules.Sum(s => s.RemainingDueAmount);

            // Update loan status to OVERDUE if still Active
            if (loan.Status == LoanStatus.Active)
            {
                loan.Status = LoanStatus.Overdue;
                loan.UpdatedAt = DateTime.UtcNow;
            }

            result.Add(new OverdueItemDto
            {
                LoanId = loan.Id,
                LoanAccountNo = loan.LoanAccountNo,
                CustomerId = loan.CustomerId,
                CustomerName = loan.Customer.FullName,
                CustomerPhone = loan.Customer.Phone,
                OutstandingAmount = loan.OutstandingAmount,
                OverdueDueAmount = overdueAmount,
                Dpd = dpd,
                Bucket = DpdBucket.Calculate(dpd),
                LastCollectionNote = loan.CollectionNotes
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => n.NoteText)
                    .FirstOrDefault()
            });
        }

        await _db.SaveChangesAsync(); // persist any status updates

        return result.OrderByDescending(x => x.Dpd);
    }

    // ── Schedule generation (§13.2 — flat interest model) ────────────────────
    private void GenerateSchedule(Loan loan, DateOnly disbursementDate)
    {
        // §12.6 Rule 4: principal_due = disbursed / tenor (flat per installment)
        // §12.6 Rule 5: interest_due = disbursed * monthly_rate (flat, constant)
        var principal = loan.ApprovedAmount;
        var rate = loan.MonthlyInterestRate;
        var n = loan.TenorMonths;

        var principalPerInstallment = Math.Round(principal / n, 2);
        var interestPerInstallment = Math.Round(principal * rate, 2);

        // §13.7 Final installment adjustment so total = original amount exactly
        var lastPrincipal = principal - principalPerInstallment * (n - 1);

        for (int i = 1; i <= n; i++)
        {
            var p = i == n ? lastPrincipal : principalPerInstallment;
            var totalDue = p + interestPerInstallment;

            // §12.6 Rule 7: first due = same calendar day next month
            // §12.6 Rule 8: next due = previous + 1 month
            _db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = p,
                InterestDue = interestPerInstallment,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = totalDue,
                Status = InstallmentStatus.Unpaid
            });
        }
    }

    // ── Loan account number generator (§LOAN-001: unique) ────────────────────
    private async Task<string> GenerateLoanAccountNoAsync()
    {
        var year = DateTime.Now.Year;
        var prefix = $"LN-{year}-";
        var last = await _db.Loans
            .Where(l => l.LoanAccountNo.StartsWith(prefix))
            .OrderByDescending(l => l.LoanAccountNo)
            .Select(l => l.LoanAccountNo)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (last is not null)
        {
            var parts = last.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var n)) seq = n + 1;
        }
        return $"{prefix}{seq:D4}";
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────
    private static LoanDto MapToDto(Loan l) => new()
    {
        Id = l.Id, LoanAccountNo = l.LoanAccountNo,
        ApplicationId = l.ApplicationId,
        CustomerId = l.CustomerId, CustomerName = l.Customer?.FullName ?? "",
        ProductName = l.Product?.ProductName ?? "",
        ApprovedAmount = l.ApprovedAmount,
        TenorMonths = l.TenorMonths,
        MonthlyInterestRate = l.MonthlyInterestRate,
        ProcessingFee = l.ProcessingFee,
        DisbursementDate = l.DisbursementDate,
        MaturityDate = l.MaturityDate,
        Status = l.Status,
        OutstandingAmount = l.OutstandingAmount,
        CreatedAt = l.CreatedAt
    };

    private static ScheduleItemDto MapSchedule(RepaymentSchedule s, DateOnly today) => new()
    {
        InstallmentNo = s.InstallmentNo,
        DueDate = s.DueDate,
        PrincipalDue = s.PrincipalDue,
        InterestDue = s.InterestDue,
        TotalDue = s.PrincipalDue + s.InterestDue,
        PrincipalPaid = s.PrincipalPaid,
        InterestPaid = s.InterestPaid,
        RemainingDueAmount = s.RemainingDueAmount,
        Status = s.Status,
        Dpd = s.DueDate < today && s.RemainingDueAmount > 0
            ? today.DayNumber - s.DueDate.DayNumber
            : 0
    };

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PaymentDate = p.PaymentDate,
        Amount = p.Amount,
        PaymentMethod = p.PaymentMethod,
        Remarks = p.Remarks,
        CreatedBy = p.CreatedBy?.FullName ?? "",
        CreatedAt = p.CreatedAt
    };
}
