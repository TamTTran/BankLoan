using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using FluentAssertions;

namespace LoanFlow.Application.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly LoanFlowDbContext _db;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new LoanFlowDbContext(options);

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Jwt:Key"]).Returns("SuperSecretKeyForTestingPurposes123!");
        configurationMock.Setup(x => x["Jwt:Issuer"]).Returns("LoanFlow.API");
        configurationMock.Setup(x => x["Jwt:Audience"]).Returns("LoanFlow.Client");
        configurationMock.Setup(x => x["Jwt:TokenLifetimeMinutes"]).Returns("60");

        _authService = new AuthService(_db, configurationMock.Object);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var password = "Password123!";
        var user = new User
        {
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Test User",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var request = new LoginRequest("testuser", password, "device1", "Test Device");

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsFailureAndIncrementsAttempts()
    {
        // Arrange
        var user = new User
        {
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
            FullName = "Test User",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var request = new LoginRequest("testuser", "WrongPassword", "device1", "Test Device");

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid credentials");

        var dbUser = await _db.Users.FirstAsync();
        dbUser.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WithFiveFailedAttempts_LocksAccount()
    {
        // Arrange
        var user = new User
        {
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
            FullName = "Test User",
            Role = "Admin",
            Status = "Active",
            FailedLoginAttempts = 4
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var request = new LoginRequest("testuser", "WrongPassword", "device1", "Test Device");

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        var dbUser = await _db.Users.FirstAsync();
        dbUser.LockoutEnd.Should().NotBeNull();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
