using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Application.DTOs.Auth;

public record LoginRequest(
    [Required] string Username, 
    [Required] string Password, 
    string? DeviceId = null, 
    string? DeviceName = null);

/// <summary>
/// Internal service response — carries the plain refresh token to the controller.
/// Controller sets it as an HttpOnly cookie and exposes only PublicLoginResponse to clients.
/// </summary>
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
    bool RequiresPasswordChange = false
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
    List<string>? Permissions = null
);

public record LogoutRequest(string? DeviceHint = null);
public record SessionDto(int Id, string DeviceName, DateTime CreatedAt, DateTime ExpiresAt, bool IsActive);

public record ChangePasswordRequest(
    [Required] string OldPassword,
    [Required] string NewPassword
);

public record VerifyMfaRequest(
    [Required] string MfaToken,
    [Required] string TotpCode
);

public record ChangeExpiredPasswordRequest(
    [Required] string MfaToken,
    [Required] string NewPassword
);

public record EnableMfaRequest(
    [Required] string TotpCode
);

public record MfaSetupResponse(
    bool IsEnabled,
    string? SecretKey,
    string? ManualEntryKey,
    string? QrCodeUri
);
