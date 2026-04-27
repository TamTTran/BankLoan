using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace LoanFlow.Application.Tests.Services;

public class CoreBankingIntegrationServiceTests : IDisposable
{
    private readonly LoanFlowDbContext _db;
    private readonly Mock<ILogger<CoreBankingIntegrationService>> _loggerMock;
    private readonly CoreBankingIntegrationService _service;

    public CoreBankingIntegrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new LoanFlowDbContext(options);

        _loggerMock = new Mock<ILogger<CoreBankingIntegrationService>>();
        _service = new CoreBankingIntegrationService(_db, _loggerMock.Object);
    }

    [Fact]
    public async Task PushJournalEntryAsync_EntryNotFound_ReturnsFailure()
    {
        // Act
        var result = await _service.PushJournalEntryAsync(999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Journal Entry not found");
    }

    [Fact]
    public async Task PushJournalEntryAsync_AlreadySynced_ReturnsFailure()
    {
        // Arrange
        var je = new JournalEntry { Id = 1, ReferenceNumber = "JE01", SyncStatus = "Synced", Description = "Test" };
        _db.JournalEntries.Add(je);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.PushJournalEntryAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Already synced");
    }

    [Fact]
    public async Task PushJournalEntryAsync_ValidPendingEntry_SyncsSuccessfully()
    {
        // Arrange
        var je = new JournalEntry { Id = 2, ReferenceNumber = "JE02", SyncStatus = "Pending", Description = "Test" };
        _db.JournalEntries.Add(je);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.PushJournalEntryAsync(2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var updatedJe = await _db.JournalEntries.FindAsync(2);
        updatedJe!.SyncStatus.Should().Be("Synced");
        updatedJe.SyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PushDailyBatchAsync_SyncsMultiplePendingEntries()
    {
        // Arrange
        var today = new DateOnly(2026, 5, 2);
        _db.JournalEntries.AddRange(
            new JournalEntry { Id = 3, BusinessDate = today, SyncStatus = "Pending", ReferenceNumber = "JE03", Description = "Test" },
            new JournalEntry { Id = 4, BusinessDate = today, SyncStatus = "Pending", ReferenceNumber = "JE04", Description = "Test" },
            new JournalEntry { Id = 5, BusinessDate = today, SyncStatus = "Synced", ReferenceNumber = "JE05", Description = "Test" } // Already synced
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.PushDailyBatchAsync(today);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var entries = await _db.JournalEntries.ToListAsync();
        entries.Count(e => e.SyncStatus == "Synced").Should().Be(3);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
