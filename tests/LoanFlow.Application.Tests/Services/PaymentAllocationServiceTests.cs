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

public class PaymentAllocationServiceTests
{
    [Fact]
    public async Task AllocatePaymentAsync_Should_AllocateToOldestScheduleFirst()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new LoanFlowDbContext(options);

        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-TEST",
            ApplicationId = 1,
            CustomerId = 1,
            ProductId = 1,
            ApprovedAmount = 1000,
            OutstandingAmount = 1000,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Group1_Standard
        };

        db.Loans.Add(loan);

        var sch1 = new RepaymentSchedule { Id = 1, LoanId = 1, InstallmentNo = 1, PrincipalDue = 100, InterestDue = 10, RemainingDueAmount = 110, Status = InstallmentStatus.Unpaid, DueDate = new DateOnly(2026, 1, 1) };
        var sch2 = new RepaymentSchedule { Id = 2, LoanId = 1, InstallmentNo = 2, PrincipalDue = 100, InterestDue = 10, RemainingDueAmount = 110, Status = InstallmentStatus.Unpaid, DueDate = new DateOnly(2026, 2, 1) };
        
        db.RepaymentSchedules.AddRange(sch1, sch2);
        await db.SaveChangesAsync();

        var lateFeeService = new Mock<ILateFeeService>();
        lateFeeService
            .Setup(x => x.CalculateLateFeesAsync(1, It.IsAny<DateOnly>()))
            .ReturnsAsync(Result.Success());
        var accountingService = new Mock<IAccountingService>();
        accountingService
            .Setup(x => x.PostJournalAsync(It.IsAny<JournalEntryDto>()))
            .ReturnsAsync(Result.Success(new JournalEntry()));
        var paymentGatewayService = new Mock<IPaymentGatewayService>();
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 1, 15, 1, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 1, 15));
        var svcFees = new LoanPaymentService(db, lateFeeService.Object, clock, accountingService.Object);
        
        var paymentResult = await svcFees.RecordPaymentAsync(1, new RecordPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 1, 15),
            Amount = 110,
            PaymentMethod = PaymentMethod.BankTransfer,
            Remarks = "Test Ref"
        }, 1);
        
        paymentResult.IsSuccess.Should().BeTrue();
        
        var updatedSch1 = await db.RepaymentSchedules.FindAsync(1);
        var updatedSch2 = await db.RepaymentSchedules.FindAsync(2);

        // Schedule 1 should be fully Paid
        updatedSch1!.Status.Should().Be(InstallmentStatus.Paid);
        updatedSch1.RemainingDueAmount.Should().Be(0);

        // Schedule 2 should be totally Unpaid
        updatedSch2!.Status.Should().Be(InstallmentStatus.Unpaid);
        updatedSch2.RemainingDueAmount.Should().Be(110);
    }

    [Fact]
    public async Task RecordPaymentAsync_Should_WriteLedgerAllocationsForCurrentPaymentOnly()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new LoanFlowDbContext(options);

        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-TEST",
            ApplicationId = 1,
            CustomerId = 1,
            ProductId = 1,
            ApprovedAmount = 1000,
            OutstandingAmount = 1000,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Group1_Standard
        };

        db.Loans.Add(loan);

        db.RepaymentSchedules.Add(new RepaymentSchedule
        {
            Id = 1,
            LoanId = 1,
            InstallmentNo = 1,
            PrincipalDue = 100,
            InterestDue = 10,
            RemainingDueAmount = 110,
            Status = InstallmentStatus.Unpaid,
            DueDate = new DateOnly(2026, 1, 1)
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
        var paymentGatewayService = new Mock<IPaymentGatewayService>();
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 1, 15, 1, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 1, 15));
        var service = new LoanPaymentService(db, lateFeeService.Object, clock, accountingService.Object);

        var firstPayment = await service.RecordPaymentAsync(1, new RecordPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 1, 15),
            Amount = 15,
            PaymentMethod = PaymentMethod.BankTransfer,
            Remarks = "First partial payment"
        }, 1);

        firstPayment.IsSuccess.Should().BeTrue();

        var secondPayment = await service.RecordPaymentAsync(1, new RecordPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 1, 16),
            Amount = 20,
            PaymentMethod = PaymentMethod.BankTransfer,
            Remarks = "Second partial payment"
        }, 1);

        secondPayment.IsSuccess.Should().BeTrue();

        var latestPayment = await db.Payments
            .OrderByDescending(p => p.Id)
            .FirstAsync();

        var latestAllocations = await db.PaymentAllocations
            .Where(a => a.PaymentId == latestPayment.Id)
            .OrderBy(a => a.Id)
            .ToListAsync();

        latestAllocations.Should().HaveCount(1);
        latestAllocations[0].AllocationType.Should().Be("Principal");
        latestAllocations[0].Amount.Should().Be(20);
    }
}
