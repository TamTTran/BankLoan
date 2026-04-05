namespace LoanFlow.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    string Username,
    string FullName,
    string Role,
    int UserId,
    int? BranchId,
    DateTime ExpiresAt
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
