using FluentAssertions;
using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class LedgerReconciliationEndToEndTests
{
    [Fact]
    public async Task RecordPaymentAsync_Should_CreateLedgerChain_And_ReconcileCleanly_EndToEnd()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new LoanFlowDbContext(options);

        db.Users.Add(new User
        {
            Id = 1,
            FullName = "Admin",
            Username = "admin",
            PasswordHash = "hash",
            Role = Roles.Admin
        });

        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-2026-0001",
            ApplicationId = 1,
            CustomerId = 1,
            ProductId = 1,
            ApprovedAmount = 1000,
            OutstandingAmount = 110,
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
            DueDate = new DateOnly(2026, 1, 15)
        });

        db.GLAccounts.AddRange(
            new GLAccount { Id = 1, AccountCode = "1001", AccountName = "Cash", AccountType = GLAccountType.Asset, Balance = 0 },
            new GLAccount { Id = 2, AccountCode = "1301", AccountName = "Loan Principal", AccountType = GLAccountType.Asset, Balance = 1000 },
            new GLAccount { Id = 3, AccountCode = "1302", AccountName = "Interest Receivable", AccountType = GLAccountType.Asset, Balance = 10 },
            new GLAccount { Id = 4, AccountCode = "1303", AccountName = "Late Fee Receivable", AccountType = GLAccountType.Asset, Balance = 0 },
            new GLAccount { Id = 5, AccountCode = "2001", AccountName = "Suspense Liability", AccountType = GLAccountType.Liability, Balance = 0 });

        await db.SaveChangesAsync();

        var utcNow = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var lateFeeService = new Mock<ILateFeeService>();
        lateFeeService
            .Setup(x => x.CalculateLateFeesAsync(1, It.IsAny<DateOnly>()))
            .ReturnsAsync(Result.Success());
        var accountingService = new AccountingService(db);
        var businessClock = new TestBusinessClock(
            utcNow: utcNow,
            localNow: new DateTime(2026, 1, 15, 16, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 1, 15));
        var loanService = new LoanService(db, lateFeeService.Object, businessClock, accountingService);

        var result = await loanService.RecordPaymentAsync(1, new RecordPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 1, 15),
            Amount = 120,
            PaymentMethod = PaymentMethod.BankTransfer,
            Remarks = "E2E payment proof"
        }, 1);

        result.IsSuccess.Should().BeTrue();

        var payment = await db.Payments.SingleAsync();
        payment.CreatedAt.Should().Be(utcNow);

        var paymentTransaction = await db.LoanTransactions.SingleAsync(t => t.TransactionType == "Payment");
        paymentTransaction.Amount.Should().Be(120);
        paymentTransaction.CreatedAt.Should().Be(utcNow);

        var allocations = await db.PaymentAllocations
            .Where(a => a.PaymentId == payment.Id)
            .OrderBy(a => a.Id)
            .ToListAsync();

        allocations.Should().HaveCount(3);
        allocations.Should().ContainSingle(a => a.AllocationType == "Interest" && a.Amount == 10 && a.ScheduleId == 1);
        allocations.Should().ContainSingle(a => a.AllocationType == "Principal" && a.Amount == 100 && a.ScheduleId == 1);
        allocations.Should().ContainSingle(a => a.AllocationType == "Suspense" && a.Amount == 10 && a.ScheduleId == null);

        var suspense = await db.SuspenseTransactions.SingleAsync();
        suspense.Amount.Should().Be(10);
        suspense.SourcePaymentId.Should().Be(payment.Id);
        suspense.SourceLoanTransactionId.Should().Be(paymentTransaction.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Lines)
            .SingleAsync();

        journal.BusinessDate.Should().Be(new DateOnly(2026, 1, 15));
        journal.Lines.Should().HaveCount(4);
        journal.Lines.Sum(l => l.DebitAmount).Should().Be(120);
        journal.Lines.Sum(l => l.CreditAmount).Should().Be(120);

        db.GLAccounts.Single(a => a.AccountCode == "1001").Balance.Should().Be(120);
        db.GLAccounts.Single(a => a.AccountCode == "1301").Balance.Should().Be(900);
        db.GLAccounts.Single(a => a.AccountCode == "1302").Balance.Should().Be(0);
        db.GLAccounts.Single(a => a.AccountCode == "2001").Balance.Should().Be(10);

        var reconciliationService = new ReconciliationService(
            db,
            new BusinessCalendarService(Options.Create(new BusinessDateOptions())),
            businessClock);

        var report = await reconciliationService.RunReconciliationAsync(new DateOnly(2026, 1, 15), 1);

        report.TotalPaymentsAmount.Should().Be(120);
        report.TotalPaymentTransactionsAmount.Should().Be(120);
        report.Exceptions.Should().BeEmpty();
        report.ExceptionsFound.Should().Be(0);
    }
}
