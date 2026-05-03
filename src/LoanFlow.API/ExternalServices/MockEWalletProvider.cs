using LoanFlow.Application.Services;
using Microsoft.Extensions.Logging;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Mock MoMo E-Wallet Provider — dùng trong môi trường Development khi UseMockEWallet=true.
///
/// Hành vi giả lập:
///   - InitiateLinkingAsync: Luôn thành công, trả OTP nonce "OTP-MOMO-{guid}"
///   - VerifyOtpAsync: OTP = "123456" → thành công, khác → thất bại
///   - ChargeAsync: Giả lập latency 300ms, trả TxId "MOMO-CHG-{timestamp}-{random}"
///   - PayoutAsync: Giả lập latency 300ms, trả TxId "MOMO-PAY-{timestamp}-{random}"
///
/// Simulated failures:
///   - WalletAccountId = "FAIL"         → lỗi liên kết
///   - WalletToken chứa "INSUFFICIENT"  → lỗi không đủ số dư
/// </summary>
public class MockMoMoEWalletProvider : IEWalletProvider
{
    private readonly ILogger<MockMoMoEWalletProvider> _logger;

    public MockMoMoEWalletProvider(ILogger<MockMoMoEWalletProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "MoMo";

    public async Task<EWalletLinkResult> InitiateLinkingAsync(string walletAccountId, string customerName)
    {
        await Task.Delay(200);
        _logger.LogInformation("[MOCK-MOMO] InitiateLinking: WalletAccountId={WalletAccountId} Customer={Name}",
            walletAccountId, customerName);

        if (walletAccountId.Equals("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[MOCK-MOMO] Simulating FAILURE for WalletAccountId=FAIL");
            return new EWalletLinkResult
            {
                IsSuccess    = false,
                ErrorMessage = "MOCK-MOMO: Tài khoản MoMo không tồn tại hoặc không hợp lệ."
            };
        }

        var otpNonce = $"OTP-MOMO-{Guid.NewGuid():N}";
        _logger.LogInformation("[MOCK-MOMO] OTP gửi đến {WalletAccountId}. Nonce={Nonce}. Dùng OTP '123456' để xác thực.",
            walletAccountId, otpNonce);

        return new EWalletLinkResult
        {
            IsSuccess      = true,
            OtpNonce       = otpNonce,
            WalletUserName = $"Ví MoMo ({walletAccountId.Substring(0, Math.Min(4, walletAccountId.Length))}***)"
        };
    }

    public async Task<EWalletVerifyResult> VerifyOtpAsync(string otpNonce, string otpCode)
    {
        await Task.Delay(150);
        _logger.LogInformation("[MOCK-MOMO] VerifyOtp: Nonce={Nonce} OtpCode={Code}", otpNonce, otpCode);

        if (otpCode != "123456")
        {
            return new EWalletVerifyResult
            {
                IsSuccess    = false,
                ErrorMessage = "MOCK-MOMO: OTP không đúng. Vui lòng kiểm tra lại mã OTP từ ứng dụng MoMo."
            };
        }

        var walletToken = $"MOMO-WTOKEN-{Guid.NewGuid():N}";
        _logger.LogInformation("[MOCK-MOMO] OTP verified. WalletToken={Token}", walletToken);

        return new EWalletVerifyResult
        {
            IsSuccess      = true,
            WalletToken    = walletToken,
            WalletUserName = "Nguyễn Văn Mock (MoMo)"
        };
    }

    public async Task<EWalletChargeResult> ChargeAsync(string walletToken, decimal amount, string description)
    {
        await Task.Delay(300);
        var txId = $"MOMO-CHG-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(10000, 99999)}";
        _logger.LogInformation("[MOCK-MOMO] Charge: Token={Token} Amount={Amount:N0} TxId={TxId}",
            walletToken, amount, txId);

        if (walletToken.Contains("INSUFFICIENT", StringComparison.OrdinalIgnoreCase))
        {
            return new EWalletChargeResult
            {
                IsSuccess    = false,
                TransactionId = txId,
                Status       = "FAILED",
                Amount       = amount,
                ProcessedAt  = DateTime.UtcNow,
                ErrorCode    = "INSUFFICIENT_BALANCE",
                ErrorMessage = "MOCK-MOMO: Số dư ví MoMo không đủ để thực hiện giao dịch."
            };
        }

        return new EWalletChargeResult
        {
            IsSuccess     = true,
            TransactionId = txId,
            Status        = "SUCCESS",
            Amount        = amount,
            ProcessedAt   = DateTime.UtcNow
        };
    }

    public async Task<EWalletChargeResult> PayoutAsync(string walletToken, decimal amount, string description)
    {
        await Task.Delay(300);
        var txId = $"MOMO-PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(10000, 99999)}";
        _logger.LogInformation("[MOCK-MOMO] Payout: Token={Token} Amount={Amount:N0} TxId={TxId}",
            walletToken, amount, txId);

        return new EWalletChargeResult
        {
            IsSuccess     = true,
            TransactionId = txId,
            Status        = "SUCCESS",
            Amount        = amount,
            ProcessedAt   = DateTime.UtcNow
        };
    }

    public async Task<EWalletChargeResult> QueryTransactionAsync(string transactionId)
    {
        await Task.Delay(100);
        _logger.LogInformation("[MOCK-MOMO] QueryTransaction: TxId={TxId}", transactionId);

        return new EWalletChargeResult
        {
            IsSuccess     = true,
            TransactionId = transactionId,
            Status        = "SUCCESS",
            Amount        = 0,
            ProcessedAt   = DateTime.UtcNow
        };
    }
}
