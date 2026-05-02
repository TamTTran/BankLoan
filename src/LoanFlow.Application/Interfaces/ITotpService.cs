using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ITotpService
{
    string GenerateSecretKey();
    string GenerateQrCodeUri(string email, string secretKey);
    bool ValidateTotp(string secretKey, string totpCode);
}
