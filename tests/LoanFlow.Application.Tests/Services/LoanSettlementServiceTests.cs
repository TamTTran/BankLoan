using FluentAssertions;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class LoanSettlementServiceTests
{
    [Fact]
    public async Task EarlySettlement_ShouldPrevent_DuplicateExecution_OnIdempotencyKey()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new LoanFlowDbContext(options);

        var loan = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN-TEST-002",
            OutstandingAmount = 1000,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Standard
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        var svc = new LoanSettlementService(db);
        
        var requestDto = new LoanFlow.Application.DTOs.Loan.EarlySettlementRequestDto 
        { 
            PaymentMethod = PaymentMethod.BankTransfer,
            ReferenceNo = "REF-SETTLE",
            IdempotencyKey = "UNIQUE-IDEMP-KEY-1"
        };
        
        var res1 = await svc.ExecuteEarlySettlementAsync(1, 1, requestDto);
        res1.IsSuccess.Should().BeTrue();
        
        // Second attempt with same key should fail or return idempotent success with same data
        var res2 = await svc.ExecuteEarlySettlementAsync(1, 1, requestDto);
        res2.IsSuccess.Should().BeFalse();
        res2.Error.Should().Contain("Idempotency key already used");
    }
}
