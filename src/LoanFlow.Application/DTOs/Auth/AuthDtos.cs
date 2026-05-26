using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Application.DTOs.Auth;

// SEC-007: Input validation attributes on all auth DTOs to prevent oversized payloads and injection
public record LoginRequest(
    [Required][MaxLength(100)][RegularExpression(@"^[a-zA-Z0-9._@\-]+$", ErrorMessage = "Username contains invalid characters.")]
    string Username,

    [Required][MaxLength(256)]
    string Password,

    [MaxLength(128)] string? DeviceId = null,
    [MaxLength(200)] string? DeviceName = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,        // plain token — controller moves to HttpOnly cookie, never serialised to JSON
    string Username,
    string FullName,
    string Role,
    int UserId,
    DateTime ExpiresAt,
    bool RequiresMfa = false,
    string? MfaToken = null,
    bool RequiresPasswordChange = false,
    // SEC-005: carries last login time from DB to controller
    DateTime? LastLoginAt = null
);

/// <summary>JSON-safe response sent to the browser — no refresh token.</summary>
public record PublicLoginResponse(
    string AccessToken,
    string Username,
    string FullName,
    string Role,
    int UserId,
    DateTime ExpiresAt,
    bool RequiresMfa = false,
    string? MfaToken = null,
    bool RequiresPasswordChange = false,
    List<string>? Permissions = null,
    // SEC-005: expose last login time so clients can detect suspicious activity
    DateTime? LastLoginAt = null
);

public record LogoutRequest([MaxLength(128)] string? DeviceHint = null);

public record SessionDto(int Id, string DeviceName, DateTime CreatedAt, DateTime ExpiresAt, bool IsActive);

public record ChangePasswordRequest(
    [Required][MaxLength(256)]
    string OldPassword,

    [Required][MinLength(12)][MaxLength(256)]
    string NewPassword
);

public record VerifyMfaRequest(
    [Required][MaxLength(2048)]
    string MfaToken,

    [Required][StringLength(6, MinimumLength = 6, ErrorMessage = "TOTP code must be exactly 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "TOTP code must be 6 numeric digits.")]
    string TotpCode
);

public record ChangeExpiredPasswordRequest(
    [Required][MaxLength(2048)]
    string MfaToken,

    [Required][MinLength(12)][MaxLength(256)]
    string NewPassword
);

public record EnableMfaRequest(
    [Required][StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "TOTP code must be 6 numeric digits.")]
    string TotpCode
);

public record MfaSetupResponse(
    bool IsEnabled,
    string? SecretKey,
    string? ManualEntryKey,
    string? QrCodeUri
);
