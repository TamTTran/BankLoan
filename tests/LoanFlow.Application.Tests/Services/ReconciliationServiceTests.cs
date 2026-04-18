using FluentAssertions;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
}
