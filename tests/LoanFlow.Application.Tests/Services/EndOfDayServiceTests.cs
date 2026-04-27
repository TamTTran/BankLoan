using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace LoanFlow.Application.Tests.Services;

public class EndOfDayServiceTests : IDisposable
{
    private readonly LoanFlowDbContext _db;
    private readonly Mock<IAccrualJobService> _accrualServiceMock;
    private readonly Mock<IOverdueJobService> _overdueServiceMock;
    private readonly Mock<IClassificationJobService> _classificationServiceMock;
    private readonly Mock<IReconciliationService> _reconciliationServiceMock;
    private readonly Mock<ICollectionService> _collectionServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ISmsService> _smsServiceMock;
    private readonly Mock<ILogger<EndOfDayService>> _loggerMock;
    private readonly Mock<IBusinessClock> _businessClockMock;
    private readonly Mock<ITransactionMonitoringService> _txnMonitoringServiceMock;
    private readonly EndOfDayService _eodService;

    public EndOfDayServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new LoanFlowDbContext(options);

        _accrualServiceMock = new Mock<IAccrualJobService>();
        _overdueServiceMock = new Mock<IOverdueJobService>();
        _classificationServiceMock = new Mock<IClassificationJobService>();
        _reconciliationServiceMock = new Mock<IReconciliationService>();
        _collectionServiceMock = new Mock<ICollectionService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _smsServiceMock = new Mock<ISmsService>();
        _loggerMock = new Mock<ILogger<EndOfDayService>>();
        _businessClockMock = new Mock<IBusinessClock>();
        _txnMonitoringServiceMock = new Mock<ITransactionMonitoringService>();

        _businessClockMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);

        _eodService = new EndOfDayService(
            _accrualServiceMock.Object,
            _overdueServiceMock.Object,
            _classificationServiceMock.Object,
            _reconciliationServiceMock.Object,
            _collectionServiceMock.Object,
            _notificationServiceMock.Object,
            _smsServiceMock.Object,
            _db,
            _loggerMock.Object,
            _businessClockMock.Object,
            _txnMonitoringServiceMock.Object);
    }

    [Fact]
    public async Task RunAsync_SuccessfulExecution_UnlocksSystem()
    {
        // Arrange
        var businessDate = new DateOnly(2026, 4, 22);

        // Act
        var result = await _eodService.RunAsync(businessDate);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().Be(0);

        var lockConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "IsEodRunning");
        lockConfig.Should().NotBeNull();
        lockConfig!.Value.Should().Be("false");
    }

    [Fact]
    public async Task RunAsync_WhenStepFails_LeavesSystemLockedAndSendsSms()
    {
        // Arrange
        var businessDate = new DateOnly(2026, 4, 22);
        
        // Add AdminPhone config
        _db.SystemConfigs.Add(new Domain.Entities.SystemConfig { Key = "AdminPhone", Value = "0987654321" });
        await _db.SaveChangesAsync();

        _accrualServiceMock.Setup(x => x.RunDailyAccrualAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated DB failure"));

        // Act
        var result = await _eodService.RunAsync(businessDate);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().Be(1);

        var lockConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "IsEodRunning");
        lockConfig.Should().NotBeNull();
        lockConfig!.Value.Should().Be("true"); // Remains locked!

        _smsServiceMock.Verify(x => x.SendAsync("0987654321", It.Is<string>(s => s.Contains("LOCKED"))), Times.Once);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
