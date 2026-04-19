using FluentAssertions;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class OverdueJobBusinessDateTests
{
    [Fact]
    public async Task OverdueJob_Should_Process_Loans_Up_To_BusinessDate()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-TEST-003",
            OutstandingAmount = 1000,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Standard
        };
        db.Loans.Add(loan);

        var sch1 = new RepaymentSchedule { Id = 1, LoanId = 1, InstallmentNo = 1, RemainingDueAmount = 100, Status = InstallmentStatus.Unpaid, DueDate = new DateOnly(2026, 4, 1) };
        var sch2 = new RepaymentSchedule { Id = 2, LoanId = 1, InstallmentNo = 2, RemainingDueAmount = 100, Status = InstallmentStatus.Unpaid, DueDate = new DateOnly(2026, 5, 1) }; // Future due date
        
        db.RepaymentSchedules.AddRange(sch1, sch2);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<OverdueJobService>>();
        var svc = new OverdueJobService(db, logger.Object);
        
        // Simulating EOD for April 15, meaning sch1 (April 1) should be flagged Overdue, but Sch2 (May 1) should not.
        var result = await svc.RunDailyOverdueProcessingAsync(new DateOnly(2026, 4, 15), 100);

        result.Success.Should().BeTrue();
        result.TotalProcessed.Should().Be(1);

        var updatedSch1 = await db.RepaymentSchedules.FindAsync(1);
        updatedSch1!.Status.Should().Be(InstallmentStatus.Overdue);

        var updatedSch2 = await db.RepaymentSchedules.FindAsync(2);
        updatedSch2!.Status.Should().Be(InstallmentStatus.Unpaid);
        
        var updatedLoan = await db.Loans.FindAsync(1);
        updatedLoan!.Status.Should().Be(LoanStatus.Overdue); // The loan itself should flip to Overdue
    }
}
