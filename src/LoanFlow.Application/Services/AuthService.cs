using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LoanFlow.Application.Services;

public class AuthService : IAuthService
{
    private readonly LoanFlowDbContext _db;
    private readonly IConfiguration _config;
    private readonly ITotpService _totpService;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int MaxConcurrentDevices = 5;

    // ── Constructor overloads (backward-compatible for tests) ────────────────

    public AuthService(LoanFlowDbContext db, IConfiguration config)
        : this(db, config, new TotpService(), null, NullLogger<AuthService>.Instance, null)
    {
    }

    public AuthService(LoanFlowDbContext db, IConfiguration config, ITotpService totpService)
        : this(db, config, totpService, null, NullLogger<AuthService>.Instance, null)
    {
    }

    public AuthService(
        LoanFlowDbContext db,
        IConfiguration config,
        INotificationService notificationService,
        ITotpService totpService,
        ILogger<AuthService> logger)
        : this(db, config, totpService, notificationService, logger, null)
    {
    }

    // ── Primary DI constructor (registered in Program.cs) ────────────────────
    public AuthService(
        LoanFlowDbContext db,
        IConfiguration config,
        ITotpService totpService,
        INotificationService? notificationService,
        ILogger<AuthService> logger,
        IHttpContextAccessor? httpContextAccessor)
    {
        _db = db;
        _config = config;
        _totpService = totpService;
        _notificationService = notificationService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    // ── SHA-256 one-way hash for DB storage ─────────────────────────────────
    public static string ComputeSha256Hash(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes); // 64-char hex, safe for VARCHAR(500)
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Status == "Active");

        if (user is null)
        {
            // Log failed attempt even for unknown usernames (avoids timing oracle but still audits)
            await WriteAuthAuditAsync(AuditActions.AuthLoginFailed, 0, $"Unknown username: {request.Username}");
            return Result.Failure<LoginResponse>("Invalid credentials.");
        }

        // ── Lockout check ────────────────────────────────────────────────────
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remaining = (int)(user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes + 1;
            await WriteAuthAuditAsync(AuditActions.AuthLoginFailed, user.Id, $"Account locked. Remaining: {remaining} min");
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
                await WriteAuthAuditAsync(AuditActions.AuthAccountLocked, user.Id, $"Locked for {LockoutMinutes} min after {MaxFailedAttempts} failed attempts");
                return Result.Failure<LoginResponse>($"Account locked for {LockoutMinutes} minutes due to too many failed attempts.");
            }

            await _db.SaveChangesAsync();
            await WriteAuthAuditAsync(AuditActions.AuthLoginFailed, user.Id, $"Wrong password. Attempt {user.FailedLoginAttempts}/{MaxFailedAttempts}");
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
            var tempToken = GenerateMfaToken(user.Id); // repurpose MFA token for secure PW change session
            return Result.Success(new LoginResponse(
                AccessToken: "",
                RefreshToken: "",
                Username: user.Username,
                FullName: user.FullName,
                Role: user.Role,
                UserId: user.Id,
                ExpiresAt: DateTime.UtcNow.AddMinutes(15),
                RequiresPasswordChange: true,
                MfaToken: tempToken // token used to authenticate change-password request
            ));
        }

        if (user.MfaEnabled)
        {
            var mfaToken = GenerateMfaToken(user.Id);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_notificationService != null && !string.IsNullOrEmpty(user.MfaSecret))
                    {
                        var otpCode = _totpService.GenerateCurrentTotp(user.MfaSecret);
                        await _notificationService.SendAsync(
                            userId: user.Id,
                            type: "MFA_OTP",
                            title: "Mã xác thực đăng nhập",
                            message: $"Mã OTP của bạn là: {otpCode}. Mã có hiệu lực trong 30 giây. Không chia sẻ mã này.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to push MFA OTP notification to user {UserId}", user.Id);
                }
            });

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

        var response = await GenerateAuthResponseAsync(user, request.DeviceId, request.DeviceName);
        if (response.IsSuccess)
            await WriteAuthAuditAsync(AuditActions.AuthLoginSuccess, user.Id, $"Device: {request.DeviceName ?? "Unknown"}");
        return response;
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
        if (!_totpService.ValidateTotp(user.MfaSecret!, request.TotpCode))
        {
            await WriteAuthAuditAsync(AuditActions.AuthLoginFailed, userId, "Invalid TOTP code during MFA verification");
            return Result.Failure<LoginResponse>("Invalid TOTP code.");
        }

        var response = await GenerateAuthResponseAsync(user, deviceId, deviceName);
        if (response.IsSuccess)
            await WriteAuthAuditAsync(AuditActions.AuthMfaVerified, userId, $"Device: {deviceName ?? "Unknown"}");
        return response;
    }

    public async Task<Result<MfaSetupResponse>> GetMfaSetupAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result.Failure<MfaSetupResponse>("User not found.");

        if (user.MfaEnabled) return Result.Success(new MfaSetupResponse(
            IsEnabled: true,
            SecretKey: null,
            ManualEntryKey: null,
            QrCodeUri: null));

        var secret = _totpService.GenerateSecretKey();
        
        user.MfaSecret = secret; // Save temporarily
        await _db.SaveChangesAsync();

        var qrUri = _totpService.GenerateQrCodeUri(user.Username, secret);
        return Result.Success(new MfaSetupResponse(
            IsEnabled: false,
            SecretKey: secret,
            ManualEntryKey: secret,
            QrCodeUri: qrUri));
    }

    public async Task<Result> EnableMfaAsync(int userId, string totpCode)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result.Failure("User not found.");
        if (user.MfaEnabled) return Result.Failure("MFA is already enabled.");
        if (string.IsNullOrEmpty(user.MfaSecret)) return Result.Failure("MFA setup not initiated.");

        if (!_totpService.ValidateTotp(user.MfaSecret, totpCode))
        {
            return Result.Failure("Invalid TOTP code.");
        }

        user.MfaEnabled = true;
        await _db.SaveChangesAsync();
        await WriteAuthAuditAsync(AuditActions.AuthMfaEnabled, userId, "MFA TOTP enabled successfully");
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
        await WriteAuthAuditAsync(AuditActions.AuthPasswordChanged, userId, "Password changed by user");

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

        var response = await GenerateAuthResponseAsync(existingToken.User, existingToken.DeviceId, existingToken.DeviceName);
        if (response.IsSuccess)
            await WriteAuthAuditAsync(AuditActions.AuthTokenRefresh, existingToken.UserId, $"Device: {existingToken.DeviceName}");
        return response;
    }

    public async Task<Result> LogoutAsync(int userId, string plainRefreshToken)
    {
        // SEC-002: Revoke only the calling device's token — NOT all sessions.
        // Use POST /api/auth/logout-all to revoke all sessions.
        var hash = ComputeSha256Hash(plainRefreshToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId);
        if (token != null)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
        await WriteAuthAuditAsync(AuditActions.AuthLogout, userId, $"Device token revoked");
        return Result.Success();
    }

    public async Task<Result<LoginResponse>> ChangeExpiredPasswordAsync(string mfaToken, string newPassword)
    {
        var principal = ValidateMfaToken(mfaToken);
        if (principal == null)
            return Result.Failure<LoginResponse>("Invalid or expired token.");

        var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdString, out int userId))
            return Result.Failure<LoginResponse>("Invalid token claims.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.Status != "Active")
            return Result.Failure<LoginResponse>("Invalid user.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordExpiresAt = DateTime.UtcNow.AddDays(90);
        
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        await _db.SaveChangesAsync();
        await LogoutAllAsync(userId);

        if (user.MfaEnabled)
        {
            var newMfaToken = GenerateMfaToken(user.Id);
            return Result.Success(new LoginResponse(
                AccessToken: "",
                RefreshToken: "",
                Username: user.Username,
                FullName: user.FullName,
                Role: user.Role,
                UserId: user.Id,
                ExpiresAt: DateTime.UtcNow.AddMinutes(5),
                RequiresMfa: true,
                MfaToken: newMfaToken
            ));
        }

        return await GenerateAuthResponseAsync(user, null, null);
    }

    public async Task<Result> LogoutAllAsync(int userId)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
            token.IsRevoked = true;

        await _db.SaveChangesAsync();
        await WriteAuthAuditAsync(AuditActions.AuthLogoutAll, userId, $"All {activeTokens.Count} session(s) revoked");
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
        await WriteAuthAuditAsync(AuditActions.AuthSessionRevoked, userId, $"Session {sessionId} ({token.DeviceName}) remotely revoked");
        return Result.Success();
    }

    private async Task<Result<LoginResponse>> GenerateAuthResponseAsync(User user, string? deviceId, string? deviceName)
    {
        // Load permissions for user role
        var permissions = await _db.RolePermissions
            .Where(rp => rp.RoleCode == user.Role)
            .Select(rp => rp.PermissionCode)
            .ToListAsync();
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
        var accessToken = GenerateJwtToken(user, lifetimeMinutes, permissions);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash, // only hash stored in DB
            DeviceId = dId,
            DeviceName = dName,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.RefreshTokens.Add(refreshTokenEntity);

        // SEC-005: Track last login time
        user.LastLoginAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
        // PlainRefreshToken is passed to controller to set as HttpOnly cookie
        return Result.Success(new LoginResponse(accessToken, plainToken, user.Username, user.FullName, user.Role, user.Id, expiresAt, LastLoginAt: user.LastLoginAt));
    }

    private string GenerateJwtToken(User user, int lifetimeMinutes, List<string> permissions)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName),
            new Claim("permissions", string.Join(",", permissions))
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

    // ── SEC-001: Centralised auth audit writer ───────────────────────────────
    private async Task WriteAuthAuditAsync(string actionCode, int userId, string? detail = null)
    {
        try
        {
            var ctx = _httpContextAccessor?.HttpContext;
            var ip  = ctx?.Connection.RemoteIpAddress?.ToString()
                      ?? ctx?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var ua  = ctx?.Request.Headers["User-Agent"].FirstOrDefault();
            var cid = ctx?.TraceIdentifier;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId    = userId > 0 ? userId : null,
                ActionCode     = actionCode,
                EntityType     = "User",
                EntityId       = userId,
                NewValuesJson  = detail is not null ? $"{{\"detail\":\"{detail}\"}}" : null,
                IpAddress      = ip,
                UserAgent      = ua,
                CorrelationId  = cid,
                CreatedAt      = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let audit failure break the auth flow
            _logger.LogWarning(ex, "Failed to write auth audit log for action {Action}, userId {UserId}", actionCode, userId);
        }
    }
}
