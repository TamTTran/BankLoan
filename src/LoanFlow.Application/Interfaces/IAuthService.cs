using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Auth;

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
    Task<Result<LoginResponse>> ChangeExpiredPasswordAsync(string mfaToken, string newPassword);
    Task<Result<LoginResponse>> VerifyMfaAsync(VerifyMfaRequest request, string? deviceId, string? deviceName);
    Task<Result<MfaSetupResponse>> GetMfaSetupAsync(int userId);
    Task<Result> EnableMfaAsync(int userId, string totpCode);
}
