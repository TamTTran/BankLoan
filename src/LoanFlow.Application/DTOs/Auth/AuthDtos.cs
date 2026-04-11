namespace LoanFlow.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password, string? DeviceId = null, string? DeviceName = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string Username,
    string FullName,
    string Role,
    int UserId,
    DateTime ExpiresAt
);

public record RefreshTokenRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record SessionDto(int Id, string DeviceName, DateTime CreatedAt, DateTime ExpiresAt, bool IsActive);
