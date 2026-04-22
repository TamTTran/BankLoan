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
    DateTime ExpiresAt
);

/// <summary>JSON-safe response sent to the browser — no refresh token.</summary>
public record PublicLoginResponse(
    string AccessToken,
    string Username,
    string FullName,
    string Role,
    int UserId,
    DateTime ExpiresAt
);

public record LogoutRequest(string? DeviceHint = null);
public record SessionDto(int Id, string DeviceName, DateTime CreatedAt, DateTime ExpiresAt, bool IsActive);
