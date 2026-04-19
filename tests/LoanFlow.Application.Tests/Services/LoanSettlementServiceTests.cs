using FluentAssertions;
using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class LoanSettlementServiceTests
{
    [Fact]
    public async Task EarlySettleAsync_Should_CloseLoan_And_CreateSettlementPayment()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-TEST-002",
            OutstandingAmount = 1_000m,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Group1_Standard,
            DisbursementDate = new DateOnly(2026, 1, 1),
            EarlySettlementFeeRate = 0
        };
        db.Loans.Add(loan);
        db.RepaymentSchedules.Add(new RepaymentSchedule
        {
            Id = 1,
            LoanId = 1,
            InstallmentNo = 1,
            DueDate = new DateOnly(2026, 2, 1),
            PrincipalDue = 1_000m,
            InterestDue = 0m,
            RemainingDueAmount = 1_000m,
            Status = InstallmentStatus.Unpaid
        });
        await db.SaveChangesAsync();

        var lateFeeService = new Mock<ILateFeeService>();
        lateFeeService
            .Setup(x => x.CalculateLateFeesAsync(1, It.IsAny<DateOnly>()))
            .ReturnsAsync(Result.Success());
        var accountingService = new Mock<IAccountingService>();
        accountingService
            .Setup(x => x.PostJournalAsync(It.IsAny<JournalEntryDto>()))
            .ReturnsAsync(Result.Success(new JournalEntry()));
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 1, 15, 1, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 1, 15));
        var svc = new LoanService(db, lateFeeService.Object, clock, accountingService.Object);

        var result = await svc.EarlySettleAsync(1, new EarlySettleRequest
        {
            SettlementDate = new DateOnly(2026, 1, 15),
            WaiveFee = false
        }, 1);

        result.IsSuccess.Should().BeTrue();

        var updatedLoan = await db.Loans.FindAsync(1);
        updatedLoan!.Status.Should().Be(LoanStatus.Closed);
        updatedLoan.OutstandingAmount.Should().Be(0);

        var payment = await db.Payments.SingleAsync(p => p.LoanId == 1);
        payment.PaymentDate.Should().Be(new DateOnly(2026, 1, 15));
    }
}
