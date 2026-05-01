using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

/// <summary>
/// Shared static helpers dùng bởi LoanQueryService, LoanDisbursementService, LoanPaymentService.
/// Giữ MapToDto, MapSchedule, MapPayment ở 1 nơi để tránh duplicate.
/// </summary>
internal static class LoanMappers
{
    internal static LoanDto MapToDto(Loan l) => new()
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
        VirtualAccountNo = l.VirtualAccountNo,
        DisbursementMethod = l.Disbursement?.DisbursementMethod,
        DisbursementStatus = l.Disbursement?.DisbursementStatus,
        NapasReferenceId = l.Disbursement?.NapasReferenceId,
        NapasTransactionId = l.Disbursement?.NapasTransactionId,
        GatewayConfirmedAt = l.Disbursement?.GatewayConfirmedAt,
        GatewayErrorMessage = l.Disbursement?.GatewayErrorMessage,
        CreatedAt = l.CreatedAt
    };

    internal static ScheduleItemDto MapSchedule(RepaymentSchedule s, DateOnly today) => new()
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

    internal static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PaymentDate = p.PaymentDate,
        Amount = p.Amount,
        PaymentMethod = p.PaymentMethod,
        Remarks = p.Remarks,
        CreatedBy = p.CreatedBy?.FullName ?? "",
        CreatedAt = p.CreatedAt
    };

    /// <summary>
    /// Load đầy đủ Loan với tất cả navigation properties cần thiết cho LoanDto.
    /// Dùng AsSplitQuery để tránh cartesian explosion khi có nhiều collections.
    /// </summary>
    internal static async Task<Loan?> LoadFullLoanAsync(LoanFlowDbContext db, int loanId)
        => await db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Product)
            .Include(l => l.Application)
            .Include(l => l.Disbursement)
            .Include(l => l.RepaymentSchedules)
            .Include(l => l.Payments).ThenInclude(p => p.CreatedBy)
            .Include(l => l.LateFees)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == loanId);
}

/// <summary>
/// Shared schedule generation logic — dùng bởi LoanDisbursementService.
/// Tách ra để có thể unit test độc lập.
/// </summary>
internal static class ScheduleGenerator
{
    /// <summary>Flat interest (lãi trên dư nợ ban đầu)</summary>
    internal static void GenerateFlat(LoanFlowDbContext db, Loan loan, DateOnly disbursementDate)
    {
        var principal = loan.ApprovedAmount;
        var rate = loan.MonthlyInterestRate;
        var n = loan.TenorMonths;

        var principalPerInstallment = Math.Round(principal / n, 2);
        var interestPerInstallment = Math.Round(principal * rate, 2);
        var lastPrincipal = principal - principalPerInstallment * (n - 1);

        for (int i = 1; i <= n; i++)
        {
            var p = i == n ? lastPrincipal : principalPerInstallment;
            db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = p,
                InterestDue = interestPerInstallment,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = p + interestPerInstallment,
                Status = InstallmentStatus.Unpaid
            });
        }
    }

    /// <summary>Reducing Balance / EMI (lãi trên dư nợ còn lại)</summary>
    internal static void GenerateReducingBalance(LoanFlowDbContext db, Loan loan, DateOnly disbursementDate)
    {
        var principal = loan.ApprovedAmount;
        var rate = loan.MonthlyInterestRate;
        var n = loan.TenorMonths;
        var grace = loan.GracePeriodMonths;
        int repaymentMonths = n - grace;

        decimal emi;
        if (rate == 0 || repaymentMonths <= 0)
            emi = repaymentMonths > 0 ? Math.Round(principal / repaymentMonths, 2) : principal;
        else
        {
            double r = (double)rate, p = (double)principal, nm = repaymentMonths;
            emi = Math.Round((decimal)(p * r * Math.Pow(1 + r, nm) / (Math.Pow(1 + r, nm) - 1)), 2);
        }

        decimal remaining = principal;
        for (int i = 1; i <= n; i++)
        {
            var effectiveRate = (loan.PromotionalRate.HasValue && i <= loan.PromotionalMonths)
                ? loan.PromotionalRate.Value : rate;
            var interestDue = Math.Round(remaining * effectiveRate, 2);

            decimal principalDue;
            if (i <= grace) principalDue = 0;
            else if (i == n) principalDue = remaining;
            else
            {
                principalDue = Math.Round(emi - interestDue, 2);
                if (principalDue > remaining) principalDue = remaining;
                if (principalDue < 0) principalDue = 0;
            }

            remaining = Math.Max(0, remaining - principalDue);
            db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = principalDue,
                InterestDue = interestDue,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = principalDue + interestDue,
                Status = InstallmentStatus.Unpaid
            });
        }
    }
}
