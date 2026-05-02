using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IEmailService
{
    Task<Result<string>> SendOtpAsync(int userId, string email, string purpose, string? deeplinkUrl = null);
    Task<Result> VerifyOtpAsync(int userId, string otpCode, string? deeplinkToken = null);
}
