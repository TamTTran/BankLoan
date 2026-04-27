using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace LoanFlow.Application.Tests.Services;

public class VirtualAccountMatchingServiceTests : IDisposable
{
    private readonly LoanFlowDbContext _db;
    private readonly Mock<ILoanService> _loanServiceMock;
    private readonly Mock<ILogger<VirtualAccountMatchingService>> _loggerMock;
    private readonly VirtualAccountMatchingService _service;

    public VirtualAccountMatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new LoanFlowDbContext(options);

        _loanServiceMock = new Mock<ILoanService>();
        _loggerMock = new Mock<ILogger<VirtualAccountMatchingService>>();

        _service = new VirtualAccountMatchingService(_db, _loanServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessIncomingPaymentAsync_LoanNotFound_MovesToSuspenseAccount()
    {
        // Arrange
        var virtualAccountNo = "VA123456";
        var amount = 1_000_000m;
        var transactionId = "TXN001";
        var paymentDate = new DateOnly(2026, 5, 1);
        var remarks = "Incoming wire";

        // Act
        var result = await _service.ProcessIncomingPaymentAsync(virtualAccountNo, amount, transactionId, paymentDate, remarks);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Moved to suspense");

        var suspense = await _db.SuspenseTransactions.FirstOrDefaultAsync();
        suspense.Should().NotBeNull();
        suspense!.Amount.Should().Be(amount);
        suspense.Reason.Should().Contain(virtualAccountNo);
        suspense.Reason.Should().Contain(transactionId);
    }

    [Fact]
    public async Task ProcessIncomingPaymentAsync_DuplicatePayment_ReturnsFailure()
    {
        // Arrange
        var transactionId = "TXN002";
        _db.Payments.Add(new Payment { ExternalReferenceId = transactionId, Amount = 1000, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), PaymentMethod = "BankTransfer", LoanId = 1 });
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ProcessIncomingPaymentAsync("VA001", 1000, transactionId, DateOnly.FromDateTime(DateTime.UtcNow), "test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Payment already processed.");
    }

    [Fact]
    public async Task ProcessIncomingPaymentAsync_ValidLoan_CallsRecordPayment()
    {
        // Arrange
        var loan = new Loan { VirtualAccountNo = "VA003", LoanAccountNo = "LN003", OutstandingAmount = 5000000m, ProductType = "Personal", CurrencyCode = "VND", Status = LoanStatus.Active };
        _db.Loans.Add(loan);
        await _db.SaveChangesAsync();

        var transactionId = "TXN003";
        _loanServiceMock.Setup(s => s.RecordPaymentAsync(loan.Id, It.IsAny<RecordPaymentRequest>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _service.ProcessIncomingPaymentAsync("VA003", 500_000m, transactionId, DateOnly.FromDateTime(DateTime.UtcNow), "Repayment");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _loanServiceMock.Verify(s => s.RecordPaymentAsync(
            loan.Id, 
            It.Is<RecordPaymentRequest>(r => r.Amount == 500_000m && r.ExternalReferenceId == transactionId), 
            1), Times.Once);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
