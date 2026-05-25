using LoanFlow.Application.Common;
using OtpNet;

namespace LoanFlow.Application.Services;

public class TotpService : ITotpService
{
    private const string Issuer = "LoanFlow";

    public string GenerateSecretKey()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCodeUri(string email, string secretKey)
    {
        return $"otpauth://totp/{Issuer}:{email}?secret={secretKey}&issuer={Issuer}";
    }

    public bool ValidateTotp(string secretKey, string totpCode)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(totpCode))
            return false;

        try
        {
            var base32Bytes = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(base32Bytes);
            
            // Allow a time window of 1 step before and after (30 seconds each)
            return totp.VerifyTotp(totpCode, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    public string GenerateCurrentTotp(string secretKey)
    {
        var base32Bytes = Base32Encoding.ToBytes(secretKey);
        var totp = new Totp(base32Bytes);
        return totp.ComputeTotp();
    }
}
