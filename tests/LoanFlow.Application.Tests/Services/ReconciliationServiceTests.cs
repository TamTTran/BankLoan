using FluentAssertions;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class ReconciliationServiceTests
{
    [Fact]
    public async Task RunReconciliationAsync_Should_UseBusinessDayUtcRange_ForTransactions()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        var user = new User { Id = 1, FullName = "Admin", Username = "admin", PasswordHash = "hash", Role = Roles.Admin };
        var customer = new Customer
        {
            Id = 1,
            FullName = "Customer",
            Phone = "0900000001",
            IdNumber = "ID001",
            Address = "HCMC",
            DateOfBirth = new DateTime(1990, 1, 1),
            CreatedByUserId = 1,
            AssignedToUserId = 1
        };
        var product = new LoanProduct { Id = 1, ProductName = "Loan Product", Status = "Active" };
        var application = new LoanApplication { Id = 1, CustomerId = 1, ProductId = 1, RequestedAmount = 1000, RequestedTenorMonths = 12, Purpose = "Test", CreatedByUserId = 1 };
        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-2026-0001",
            ApplicationId = 1,
            CustomerId = 1,
            ProductId = 1,
            ApprovedAmount = 1000,
            TenorMonths = 12,
            MonthlyInterestRate = 0.01m,
            ProcessingFee = 0,
            OutstandingAmount = 0,
            Status = LoanStatus.Active
        };

        db.Users.Add(user);
        db.Customers.Add(customer);
        db.LoanProducts.Add(product);
        db.LoanApplications.Add(application);
        db.Loans.Add(loan);
        db.Payments.Add(new Payment
        {
            Id = 1,
            LoanId = 1,
            PaymentDate = new DateOnly(2026, 4, 18),
            Amount = 100m,
            PaymentMethod = PaymentMethod.Cash,
            CreatedByUserId = 1
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            Id = 1,
            LoanId = 1,
            PaymentId = 1,
            TransactionType = "Payment",
            Amount = 100m,
            CreatedAt = new DateTime(2026, 4, 18, 9, 30, 0, DateTimeKind.Utc)
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            Id = 2,
            LoanId = 1,
            TransactionType = "Payment",
            Amount = 999m,
            CreatedAt = new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var calendar = new BusinessCalendarService(Options.Create(new BusinessDateOptions()));
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 4, 18, 19, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 4, 18));
        var service = new ReconciliationService(db, calendar, clock);

        var report = await service.RunReconciliationAsync(new DateOnly(2026, 4, 18), 1);

        report.TotalPaymentsAmount.Should().Be(100m);
        report.TotalPaymentTransactionsAmount.Should().Be(100m);
        report.Exceptions.Should().NotContain(x => x.ExceptionType == ReconciliationExceptionType.PaymentMismatch);
    }

    [Fact]
    public async Task RunReconciliationAsync_Should_Close_OpenException_WhenMismatchIsResolved()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        db.Users.Add(new User { Id = 1, FullName = "Admin", Username = "admin", PasswordHash = "hash", Role = Roles.Admin });
        db.ReconciliationExceptions.Add(new ReconciliationException
        {
            Id = 1,
            BusinessDate = new DateOnly(2026, 4, 18),
            ExceptionType = ReconciliationExceptionType.PaymentMismatch,
            Status = ReconciliationStatus.Open,
            ExpectedAmount = 100m,
            ActualAmount = 80m,
            Difference = 20m,
            Description = "Existing mismatch"
        });
        db.Payments.Add(new Payment
        {
            Id = 1,
            LoanId = 1,
            PaymentDate = new DateOnly(2026, 4, 18),
            Amount = 100m,
            PaymentMethod = PaymentMethod.Cash,
            CreatedByUserId = 1
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            Id = 1,
            LoanId = 1,
            TransactionType = "Payment",
            Amount = 100m,
            CreatedAt = new DateTime(2026, 4, 18, 9, 59, 0, DateTimeKind.Utc)
        });
        db.PaymentAllocations.Add(new PaymentAllocation
        {
            Id = 1,
            PaymentId = 1,
            LoanTransactionId = 1,
            AllocationType = "Principal",
            Amount = 100m
        });
        await db.SaveChangesAsync();

        var calendar = new BusinessCalendarService(Options.Create(new BusinessDateOptions()));
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 4, 18, 19, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 4, 18));
        var service = new ReconciliationService(db, calendar, clock);

        var report = await service.RunReconciliationAsync(new DateOnly(2026, 4, 18), 1);

        report.Exceptions.Should().BeEmpty();

        var updated = await db.ReconciliationExceptions.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(ReconciliationStatus.Resolved);
        updated.ResolvedByUserId.Should().Be(1);
        updated.Resolution.Should().Contain("resolved");
    }

    [Fact]
    public async Task RunReconciliationAsync_Should_Report_ChainMismatches_ForAllocationReversalFeeAndSuspense()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        db.Users.Add(new User { Id = 1, FullName = "Admin", Username = "admin", PasswordHash = "hash", Role = Roles.Admin });
        db.Loans.Add(new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-2026-0001",
            ApplicationId = 1,
            CustomerId = 1,
            ProductId = 1,
            ApprovedAmount = 1000,
            OutstandingAmount = 80m,
            Status = LoanStatus.Active
        });
        db.RepaymentSchedules.Add(new RepaymentSchedule
        {
            Id = 1,
            LoanId = 1,
            InstallmentNo = 1,
            PrincipalDue = 100m,
            InterestDue = 10m,
            PrincipalPaid = 20m,
            InterestPaid = 0m,
            RemainingDueAmount = 90m,
            Status = InstallmentStatus.Partial,
            DueDate = new DateOnly(2026, 4, 18)
        });

        db.Payments.Add(new Payment
        {
            Id = 1,
            LoanId = 1,
            PaymentDate = new DateOnly(2026, 4, 18),
            Amount = 100m,
            PaymentMethod = PaymentMethod.Cash,
            CreatedByUserId = 1
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            Id = 1,
            LoanId = 1,
            PaymentId = 1,
            TransactionType = "Payment",
            Amount = 100m,
            CreatedAt = new DateTime(2026, 4, 18, 9, 0, 0, DateTimeKind.Utc)
        });
        db.PaymentAllocations.Add(new PaymentAllocation
        {
            Id = 1,
            PaymentId = 1,
            LoanTransactionId = 1,
            ScheduleId = 1,
            AllocationType = "Principal",
            Amount = 80m
        });

        db.TransactionReversals.Add(new TransactionReversal
        {
            Id = 1,
            OriginalTransactionId = 1,
            ReversalTransactionId = 2,
            LoanId = 1,
            ReversalType = "PaymentReversal",
            OriginalAmount = 100m,
            ReversedAmount = 50m,
            Reason = "Test",
            Status = ReversalStatus.Approved,
            RequestedByUserId = 1,
            ApprovedByUserId = 1,
            ApprovedAt = new DateTime(2026, 4, 18, 9, 30, 0, DateTimeKind.Utc)
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            Id = 2,
            LoanId = 1,
            TransactionType = "PaymentReversal",
            Amount = -40m,
            CreatedAt = new DateTime(2026, 4, 18, 9, 30, 0, DateTimeKind.Utc)
        });

        db.LateFees.Add(new LateFee
        {
            Id = 1,
            LoanId = 1,
            ScheduleId = 1,
            OverdueAmount = 100m,
            DaysOverdue = 5,
            FeeRate = 0.1m,
            FeeAmount = 12m,
            Status = LateFeeStatus.Pending,
            AccrualLoanTransactionId = 3,
            CreatedAt = new DateTime(2026, 4, 18, 8, 0, 0, DateTimeKind.Utc),
            CalculatedAt = new DateTime(2026, 4, 18, 8, 0, 0, DateTimeKind.Utc)
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            Id = 3,
            LoanId = 1,
            TransactionType = "LateFeeAccrual",
            Amount = 10m,
            CreatedAt = new DateTime(2026, 4, 18, 8, 0, 0, DateTimeKind.Utc)
        });

        db.PaymentAllocations.Add(new PaymentAllocation
        {
            Id = 2,
            PaymentId = 1,
            LoanTransactionId = 1,
            AllocationType = "Suspense",
            Amount = 10m
        });
        db.SuspenseTransactions.Add(new SuspenseTransaction
        {
            Id = 1,
            LoanId = 1,
            Amount = 8m,
            Status = SuspenseStatus.Pending,
            SourcePaymentId = 1,
            SourceLoanTransactionId = 1
        });

        await db.SaveChangesAsync();

        var calendar = new BusinessCalendarService(Options.Create(new BusinessDateOptions()));
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 4, 18, 19, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 4, 18));
        var service = new ReconciliationService(db, calendar, clock);

        var report = await service.RunReconciliationAsync(new DateOnly(2026, 4, 18), 1);

        report.Exceptions.Should().Contain(x => x.ExceptionType == ReconciliationExceptionType.AllocationMismatch);
        report.Exceptions.Should().Contain(x => x.ExceptionType == ReconciliationExceptionType.ReversalMismatch);
        report.Exceptions.Should().Contain(x => x.ExceptionType == ReconciliationExceptionType.LateFeeMismatch);
        report.Exceptions.Should().Contain(x => x.ExceptionType == ReconciliationExceptionType.SuspenseMismatch);
        report.Exceptions.Should().Contain(x => x.ExceptionType == ReconciliationExceptionType.OutstandingMismatch);

        report.TotalPaymentsAmount.Should().Be(100m);
        report.TotalPaymentAllocationsAmount.Should().Be(90m);
        report.TotalReversalsAmount.Should().Be(50m);
        report.TotalReversalTransactionsAmount.Should().Be(40m);
        report.TotalLateFeesAmount.Should().Be(12m);
        report.TotalLateFeeTransactionsAmount.Should().Be(10m);
        report.TotalSuspenseAllocationsAmount.Should().Be(10m);
        report.TotalSuspenseTransactionsAmount.Should().Be(8m);
    }
}
