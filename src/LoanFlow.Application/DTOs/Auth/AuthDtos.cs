namespace LoanFlow.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    string Username,
    string FullName,
    string Role,
    int UserId,
    DateTime ExpiresAt
);
