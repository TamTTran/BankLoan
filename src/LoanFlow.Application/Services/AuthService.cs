using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LoanFlow.Application.Services;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request);
    Task<Result<LoginResponse>> RefreshAsync(string refreshToken);
    Task<Result> LogoutAsync(string refreshToken);
    Task<Result> LogoutAllAsync(int userId);
    Task<IEnumerable<SessionDto>> GetSessionsAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly LoanFlowDbContext _db;
    private readonly IConfiguration _config;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int MaxConcurrentDevices = 5;

    public AuthService(LoanFlowDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Status == "Active");

        if (user is null)
            return Result.Failure<LoginResponse>("Invalid credentials.");

        // ── Lockout check ────────────────────────────────────────────────────
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remaining = (int)(user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes + 1;
            return Result.Failure<LoginResponse>($"Account temporarily locked. Try again in {remaining} minute(s).");
        }

        // ── Password verification ────────────────────────────────────────────
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                user.FailedLoginAttempts = 0; // reset counter after locking
                await _db.SaveChangesAsync();
                return Result.Failure<LoginResponse>($"Account locked for {LockoutMinutes} minutes due to too many failed attempts.");
            }

            await _db.SaveChangesAsync();
            return Result.Failure<LoginResponse>("Invalid credentials.");
        }

        // ── Success — reset lockout state ────────────────────────────────────
        if (user.FailedLoginAttempts > 0 || user.LockoutEnd.HasValue)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
        }

        return await GenerateAuthResponseAsync(user, request.DeviceId, request.DeviceName);
    }

    public async Task<Result<LoginResponse>> RefreshAsync(string refreshToken)
    {
        var existingToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (existingToken is null || !existingToken.IsActive || existingToken.User.Status != "Active")
            return Result.Failure<LoginResponse>("Invalid or expired refresh token.");

        // Revoke current token (Refresh Token Rotation)
        existingToken.IsRevoked = true;

        // Generate new session using same device info
        return await GenerateAuthResponseAsync(existingToken.User, existingToken.DeviceId, existingToken.DeviceName);
    }

    public async Task<Result> LogoutAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token != null)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
        return Result.Success();
    }

    public async Task<Result> LogoutAllAsync(int userId)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
            token.IsRevoked = true;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<IEnumerable<SessionDto>> GetSessionsAsync(int userId)
    {
        var sessions = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return sessions.Select(s => new SessionDto(s.Id, s.DeviceName, s.CreatedAt, s.ExpiresAt, s.IsActive));
    }

    private async Task<Result<LoginResponse>> GenerateAuthResponseAsync(User user, string? deviceId, string? deviceName)
    {
        var dId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString() : deviceId;
        var dName = string.IsNullOrWhiteSpace(deviceName) ? "Unknown Device" : deviceName;

        // Enforce Max concurrent devices
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .OrderBy(t => t.CreatedAt) // ascending to revoke oldest first if needed
            .ToListAsync();

        // If we are about to exceed max devices, revoke the oldest
        if (activeTokens.Count >= MaxConcurrentDevices)
        {
            int toRevoke = activeTokens.Count - MaxConcurrentDevices + 1;
            for (int i = 0; i < toRevoke; i++)
                activeTokens[i].IsRevoked = true;
        }

        // Generate tokens
        var lifetimeMinutes = int.TryParse(_config["Jwt:TokenLifetimeMinutes"], out var parsed) ? parsed : 60;
        var accessToken = GenerateJwtToken(user, lifetimeMinutes);
        var refreshTokenString = GenerateRefreshTokenString();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenString,
            DeviceId = dId,
            DeviceName = dName,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(); // Save user lockout reset and new token

        var expiresAt = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
        return Result.Success(new LoginResponse(accessToken, refreshTokenString, user.Username, user.FullName, user.Role, user.Id, expiresAt));
    }

    private string GenerateJwtToken(User user, int lifetimeMinutes)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshTokenString()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
