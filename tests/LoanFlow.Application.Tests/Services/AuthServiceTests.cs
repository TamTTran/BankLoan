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

    // ── Login ────────────────────────────────────────────────────────────────

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
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_SetsLastLoginAt()
    {
        // SEC-005: Verify LastLoginAt is set after successful login
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

        var before = DateTime.UtcNow;
        await _authService.LoginAsync(new LoginRequest("testuser", password, "d1", "Dev"));

        var dbUser = await _db.Users.FirstAsync();
        dbUser.LastLoginAt.Should().NotBeNull();
        dbUser.LastLoginAt.Should().BeOnOrAfter(before);
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

        // Act
        var result = await _authService.LoginAsync(new LoginRequest("testuser", "WrongPassword", "d1", "Dev"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid credentials");

        var dbUser = await _db.Users.FirstAsync();
        dbUser.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WithFiveFailedAttempts_LocksAccount()
    {
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

        var result = await _authService.LoginAsync(new LoginRequest("testuser", "WrongPassword", "d1", "Dev"));

        result.IsSuccess.Should().BeFalse();
        var dbUser = await _db.Users.FirstAsync();
        dbUser.LockoutEnd.Should().NotBeNull();
        dbUser.LockoutEnd.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_WhenAccountLocked_ReturnsLockoutMessage()
    {
        var user = new User
        {
            Username = "locked",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd123!"),
            FullName = "Locked User",
            Role = "Admin",
            Status = "Active",
            LockoutEnd = DateTime.UtcNow.AddMinutes(10)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = await _authService.LoginAsync(new LoginRequest("locked", "P@ssw0rd123!", "d1", "Dev"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("locked");
    }

    // ── Refresh Token Rotation ────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesToken()
    {
        // Arrange — login first to get a refresh token
        var password = "Password123!";
        var user = new User
        {
            Username = "refreshuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Refresh User",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var loginResult = await _authService.LoginAsync(new LoginRequest("refreshuser", password, "d1", "Dev"));
        loginResult.IsSuccess.Should().BeTrue();

        var oldPlainToken = loginResult.Value.RefreshToken;

        // Act
        var refreshResult = await _authService.RefreshAsync(oldPlainToken);

        // Assert — new tokens returned
        refreshResult.IsSuccess.Should().BeTrue();
        refreshResult.Value.RefreshToken.Should().NotBe(oldPlainToken);
        refreshResult.Value.AccessToken.Should().NotBeNullOrEmpty();

        // Old token must be revoked in DB
        var oldHash = AuthService.ComputeSha256Hash(oldPlainToken);
        var oldDbToken = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == oldHash);
        oldDbToken.Should().NotBeNull();
        oldDbToken!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_WithRevokedToken_ReturnsFailure()
    {
        // Arrange
        var user = new User
        {
            Username = "user2",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass123!"),
            FullName = "User Two",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var loginResult = await _authService.LoginAsync(new LoginRequest("user2", "Pass123!", "d1", "Dev"));
        var plainToken = loginResult.Value.RefreshToken;

        // Revoke the token manually
        var hash = AuthService.ComputeSha256Hash(plainToken);
        var dbToken = await _db.RefreshTokens.FirstAsync(t => t.TokenHash == hash);
        dbToken.IsRevoked = true;
        await _db.SaveChangesAsync();

        // Act
        var result = await _authService.RefreshAsync(plainToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_RevokesOnlyCurrentDeviceToken()
    {
        // SEC-002: Logout one device must NOT revoke other device sessions
        var password = "Password123!";
        var user = new User
        {
            Username = "multidevice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Multi Device",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Login from device 1 and device 2
        var device1 = await _authService.LoginAsync(new LoginRequest("multidevice", password, "d1", "Device One"));
        var device2 = await _authService.LoginAsync(new LoginRequest("multidevice", password, "d2", "Device Two"));

        device1.IsSuccess.Should().BeTrue();
        device2.IsSuccess.Should().BeTrue();

        // Logout device 1
        await _authService.LogoutAsync(user.Id, device1.Value.RefreshToken);

        // Device 1 token should be revoked
        var hash1 = AuthService.ComputeSha256Hash(device1.Value.RefreshToken);
        var token1 = await _db.RefreshTokens.FirstAsync(t => t.TokenHash == hash1);
        token1.IsRevoked.Should().BeTrue();

        // Device 2 token should still be active
        var hash2 = AuthService.ComputeSha256Hash(device2.Value.RefreshToken);
        var token2 = await _db.RefreshTokens.FirstAsync(t => t.TokenHash == hash2);
        token2.IsRevoked.Should().BeFalse("SEC-002: logout from device 1 must not revoke device 2 session");
    }

    [Fact]
    public async Task LogoutAllAsync_RevokesAllActiveTokens()
    {
        var password = "Password123!";
        var user = new User
        {
            Username = "logoutall",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Logout All",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Login from 3 devices
        var d1 = await _authService.LoginAsync(new LoginRequest("logoutall", password, "d1", "Device One"));
        var d2 = await _authService.LoginAsync(new LoginRequest("logoutall", password, "d2", "Device Two"));
        var d3 = await _authService.LoginAsync(new LoginRequest("logoutall", password, "d3", "Device Three"));

        // Logout all
        await _authService.LogoutAllAsync(user.Id);

        // All tokens must be revoked
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .CountAsync();
        activeTokens.Should().Be(0);
    }

    // ── Session Revoke ────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeSessionAsync_WithValidSession_Succeeds()
    {
        // SEC-003: Specific session can be revoked by session ID
        var password = "Password123!";
        var user = new User
        {
            Username = "sessionuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Session User",
            Role = "Admin",
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _authService.LoginAsync(new LoginRequest("sessionuser", password, "d1", "Dev"));

        var session = await _db.RefreshTokens.FirstAsync(t => t.UserId == user.Id);

        var result = await _authService.RevokeSessionAsync(user.Id, session.Id);

        result.IsSuccess.Should().BeTrue();
        var dbToken = await _db.RefreshTokens.FindAsync(session.Id);
        dbToken!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeSessionAsync_WithOtherUsersSession_ReturnsFailure()
    {
        // Security: User A cannot revoke User B's session
        var password = "Password123!";
        var userA = new User { Username = "userA", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), FullName = "A", Role = "Admin", Status = "Active" };
        var userB = new User { Username = "userB", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), FullName = "B", Role = "Admin", Status = "Active" };
        _db.Users.AddRange(userA, userB);
        await _db.SaveChangesAsync();

        await _authService.LoginAsync(new LoginRequest("userB", password, "d1", "Dev"));
        var sessionB = await _db.RefreshTokens.FirstAsync(t => t.UserId == userB.Id);

        // User A tries to revoke User B's session
        var result = await _authService.RevokeSessionAsync(userA.Id, sessionB.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    // ── Password ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_WithWeakPassword_ReturnsFailure()
    {
        var user = new User { Username = "test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldPassword"), FullName = "Test User", Role = "Admin", Status = "Active" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = await _authService.ChangePasswordAsync(user.Id, "oldPassword", "weak");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("12 ký tự");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectPassword_RevokesAllSessions()
    {
        // Changing password should invalidate all existing sessions
        var password = "OldP@ssw0rd123!";
        var user = new User { Username = "changepw", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), FullName = "ChangePw User", Role = "Admin", Status = "Active" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Login from 2 devices
        await _authService.LoginAsync(new LoginRequest("changepw", password, "d1", "Dev1"));
        await _authService.LoginAsync(new LoginRequest("changepw", password, "d2", "Dev2"));

        // Change password
        var result = await _authService.ChangePasswordAsync(user.Id, password, "NewP@ssw0rd123!");

        result.IsSuccess.Should().BeTrue();

        // All sessions should be revoked
        var activeCount = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .CountAsync();
        activeCount.Should().Be(0, "changing password must invalidate all sessions");
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
