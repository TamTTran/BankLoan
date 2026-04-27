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
    Task<Result<LoginResponse>> RefreshAsync(string plainRefreshToken);
    Task<Result> LogoutAsync(int userId, string plainRefreshToken);
    Task<Result> LogoutAllAsync(int userId);
    Task<IEnumerable<SessionDto>> GetSessionsAsync(int userId);
    Task<Result> RevokeSessionAsync(int userId, int sessionId);
    Task<Result> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    Task<Result<LoginResponse>> VerifyMfaAsync(VerifyMfaRequest request, string? deviceId, string? deviceName);
    Task<Result<MfaSetupResponse>> GetMfaSetupAsync(int userId);
    Task<Result> EnableMfaAsync(int userId, string totpCode);
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

    // ── SHA-256 one-way hash for DB storage ─────────────────────────────────
    internal static string ComputeSha256Hash(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes); // 64-char hex, safe for VARCHAR(500)
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
                user.FailedLoginAttempts = 0;
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

        // ── Check password expiration ────────────────────────────────────────
        if (user.PasswordExpiresAt.HasValue && user.PasswordExpiresAt.Value < DateTime.UtcNow)
        {
            // Note: In real system, we'd return a specific status code like "PasswordExpired" to redirect user
            // For now, we still allow login but frontend should handle password change enforcement.
            // Result could include a flag `RequiresPasswordChange`.
        }

        if (user.MfaEnabled)
        {
            var mfaToken = GenerateMfaToken(user.Id);
            return Result.Success(new LoginResponse(
                AccessToken: "",
                RefreshToken: "",
                Username: user.Username,
                FullName: user.FullName,
                Role: user.Role,
                UserId: user.Id,
                ExpiresAt: DateTime.UtcNow.AddMinutes(5),
                RequiresMfa: true,
                MfaToken: mfaToken
            ));
        }

        return await GenerateAuthResponseAsync(user, request.DeviceId, request.DeviceName);
    }

    public async Task<Result<LoginResponse>> VerifyMfaAsync(VerifyMfaRequest request, string? deviceId, string? deviceName)
    {
        // 1. Verify MfaToken
        var principal = ValidateMfaToken(request.MfaToken);
        if (principal == null)
            return Result.Failure<LoginResponse>("Invalid or expired MFA token.");

        var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdString, out int userId))
            return Result.Failure<LoginResponse>("Invalid MFA token claims.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.Status != "Active" || !user.MfaEnabled)
            return Result.Failure<LoginResponse>("Invalid user or MFA not enabled.");

        // 2. Verify TOTP code
        // (Assuming ITotpService is injected. Wait, we need to inject ITotpService!)
        // I will just instantiate TotpService for simplicity here or add it to constructor.
        var totpService = new TotpService();
        if (!totpService.ValidateTotp(user.MfaSecret!, request.TotpCode))
        {
            return Result.Failure<LoginResponse>("Invalid TOTP code.");
        }

        return await GenerateAuthResponseAsync(user, deviceId, deviceName);
    }

    public async Task<Result<MfaSetupResponse>> GetMfaSetupAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result.Failure<MfaSetupResponse>("User not found.");

        if (user.MfaEnabled) return Result.Failure<MfaSetupResponse>("MFA is already enabled.");

        var totpService = new TotpService();
        var secret = totpService.GenerateSecretKey();
        
        user.MfaSecret = secret; // Save temporarily
        await _db.SaveChangesAsync();

        var qrUri = totpService.GenerateQrCodeUri(user.Username, secret);
        return Result.Success(new MfaSetupResponse(secret, qrUri));
    }

    public async Task<Result> EnableMfaAsync(int userId, string totpCode)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result.Failure("User not found.");
        if (user.MfaEnabled) return Result.Failure("MFA is already enabled.");
        if (string.IsNullOrEmpty(user.MfaSecret)) return Result.Failure("MFA setup not initiated.");

        var totpService = new TotpService();
        if (!totpService.ValidateTotp(user.MfaSecret, totpCode))
        {
            return Result.Failure("Invalid TOTP code.");
        }

        user.MfaEnabled = true;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _db.Users
            .Include(u => u.PasswordHistories)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || user.Status != "Active")
            return Result.Failure("User not found or inactive.");

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            return Result.Failure("Mật khẩu cũ không chính xác.");

        // ── Password Policy Validation ─────────────────────────────────────────
        if (newPassword.Length < 12)
            return Result.Failure("Mật khẩu phải có ít nhất 12 ký tự.");
        
        if (!newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower) || !newPassword.Any(char.IsDigit) || !newPassword.Any(ch => !char.IsLetterOrDigit(ch)))
            return Result.Failure("Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt.");

        // ── Check Password History (no reuse of last 5 passwords) ──────────────
        var recentPasswords = user.PasswordHistories.OrderByDescending(p => p.CreatedAt).Take(5).ToList();
        
        // Also check current password just in case it's not in history
        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
            return Result.Failure("Mật khẩu mới không được trùng với mật khẩu hiện tại.");

        foreach (var history in recentPasswords)
        {
            if (BCrypt.Net.BCrypt.Verify(newPassword, history.PasswordHash))
                return Result.Failure("Mật khẩu mới không được trùng với 5 mật khẩu gần nhất.");
        }

        // ── Apply new password ────────────────────────────────────────────────
        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordHash = newHash;
        user.LastPasswordChangeAt = DateTime.UtcNow;
        user.PasswordExpiresAt = DateTime.UtcNow.AddDays(90);

        _db.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = newHash,
            CreatedAt = DateTime.UtcNow
        });

        // Optionally, remove old histories beyond 5 to keep DB clean
        var toRemove = user.PasswordHistories.OrderByDescending(p => p.CreatedAt).Skip(4).ToList();
        if (toRemove.Any())
        {
            _db.PasswordHistories.RemoveRange(toRemove);
        }

        await _db.SaveChangesAsync();

        // Revoke all sessions to force re-login with new password
        await LogoutAllAsync(user.Id);

        return Result.Success();
    }

    public async Task<Result<LoginResponse>> RefreshAsync(string plainRefreshToken)
    {
        var hash = ComputeSha256Hash(plainRefreshToken);
        var existingToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (existingToken is null || !existingToken.IsActive || existingToken.User.Status != "Active")
            return Result.Failure<LoginResponse>("Invalid or expired refresh token.");

        // Revoke current token (Refresh Token Rotation)
        existingToken.IsRevoked = true;

        return await GenerateAuthResponseAsync(existingToken.User, existingToken.DeviceId, existingToken.DeviceName);
    }

    public async Task<Result> LogoutAsync(int userId, string plainRefreshToken)
    {
        // Scope to caller's own tokens only — prevents cross-user session denial
        var hash = ComputeSha256Hash(plainRefreshToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId);
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

    public async Task<Result> RevokeSessionAsync(int userId, int sessionId)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == sessionId);
        if (token is null) return Result.Failure("Session not found or does not belong to user.");
        
        token.IsRevoked = true;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<Result<LoginResponse>> GenerateAuthResponseAsync(User user, string? deviceId, string? deviceName)
    {
        var dId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString() : deviceId;
        var dName = string.IsNullOrWhiteSpace(deviceName) ? "Unknown Device" : deviceName;

        // Enforce Max concurrent devices
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        if (activeTokens.Count >= MaxConcurrentDevices)
        {
            int toRevoke = activeTokens.Count - MaxConcurrentDevices + 1;
            for (int i = 0; i < toRevoke; i++)
                activeTokens[i].IsRevoked = true;
        }

        // Generate plain token (returned to client via HttpOnly cookie)
        var plainToken = GeneratePlainRefreshToken();
        var tokenHash = ComputeSha256Hash(plainToken);

        var lifetimeMinutes = int.TryParse(_config["Jwt:TokenLifetimeMinutes"], out var parsed) ? parsed : 60;
        var accessToken = GenerateJwtToken(user, lifetimeMinutes);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash, // only hash stored in DB
            DeviceId = dId,
            DeviceName = dName,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
        // PlainRefreshToken is passed to controller to set as HttpOnly cookie
        return Result.Success(new LoginResponse(accessToken, plainToken, user.Username, user.FullName, user.Role, user.Id, expiresAt));
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

    private string GenerateMfaToken(int userId)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("purpose", "mfa")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5), // 5 minutes validity
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateMfaToken(string mfaToken)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(mfaToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            if (principal.FindFirst("purpose")?.Value != "mfa") return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static string GeneratePlainRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
