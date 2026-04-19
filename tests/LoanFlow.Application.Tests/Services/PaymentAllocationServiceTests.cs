using FluentAssertions;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class PaymentAllocationServiceTests
{
    [Fact]
    public async Task AllocatePaymentAsync_Should_AllocateToOldestScheduleFirst()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
            CurrentClassification = LoanClassification.Standard
        };

        db.Loans.Add(loan);

        var sch1 = new RepaymentSchedule { Id = 1, LoanId = 1, InstallmentNo = 1, PrincipalDue = 100, InterestDue = 10, RemainingDueAmount = 110, Status = InstallmentStatus.Unpaid, DueDate = new DateOnly(2026, 1, 1) };
        var sch2 = new RepaymentSchedule { Id = 2, LoanId = 1, InstallmentNo = 2, PrincipalDue = 100, InterestDue = 10, RemainingDueAmount = 110, Status = InstallmentStatus.Unpaid, DueDate = new DateOnly(2026, 2, 1) };
        
        db.RepaymentSchedules.AddRange(sch1, sch2);
        await db.SaveChangesAsync();

        // Need to test allocation service. Usually Payment is handled by IPaymentService
        var svcFees = new PaymentService(db);
        
        var paymentResult = await svcFees.ReceivePaymentAsync(1, 110, PaymentMethod.BankTransfer, "Test Ref", 1);
        
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
}
