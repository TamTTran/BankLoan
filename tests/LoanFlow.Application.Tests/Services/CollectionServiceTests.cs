using FluentAssertions;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class CollectionServiceTests
{
    [Fact]
    public async Task CheckBrokenPromisesAsync_Should_MarkPromiseBroken_WhenPaymentsDoNotMeetPromisedAmount()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        db.CollectionActions.Add(new CollectionAction
        {
            Id = 1,
            LoanId = 10,
            CollectorUserId = 1,
            ActionType = CollectionActionType.Call,
            ActionDate = new DateOnly(2026, 4, 10),
            Outcome = CollectionOutcome.PromiseToPay,
            PromiseDate = new DateOnly(2026, 4, 15),
            PromiseAmount = 1_000m,
            PromiseStatus = PromiseStatus.Pending
        });
        db.Payments.Add(new Payment
        {
            Id = 1,
            LoanId = 10,
            PaymentDate = new DateOnly(2026, 4, 12),
            Amount = 300m,
            PaymentMethod = PaymentMethod.Cash,
            CreatedByUserId = 1
        });
        db.Payments.Add(new Payment
        {
            Id = 2,
            LoanId = 10,
            PaymentDate = new DateOnly(2026, 4, 16),
            Amount = 1_500m,
            PaymentMethod = PaymentMethod.Cash,
            CreatedByUserId = 1
        });
        await db.SaveChangesAsync();

        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 4, 16, 1, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 4, 16, 8, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 4, 16));
        var service = new CollectionService(db, clock);

        var brokenCount = await service.CheckBrokenPromisesAsync(new DateOnly(2026, 4, 16));

        brokenCount.Should().Be(1);

        var action = await db.CollectionActions.FindAsync(1);
        action!.PromiseStatus.Should().Be(PromiseStatus.Broken);
    }
}
