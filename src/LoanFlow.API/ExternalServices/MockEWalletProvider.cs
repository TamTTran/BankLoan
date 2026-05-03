using LoanFlow.Application.Services;
using Microsoft.Extensions.Logging;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Mock E-Wallet Provider — dùng trong môi trường Development.
///
/// Hành vi giả lập:
///   - InitiateLinkingAsync: Luôn thành công, trả OTP nonce
///   - VerifyOtpAsync: OTP = "123456" → thành công, khác → thất bại
///   - ChargeAsync: Trừ tiền ví, giả lập latency 300ms
///   - PayoutAsync: Chuyển tiền vào ví, giả lập latency 300ms
///
/// Simulated failures:
///   - WalletAccountId = "FAIL" → lỗi liên kết
///   - WalletAccountId = "INSUFFICIENT" → lỗi không đủ số dư
/// </summary>
public class MockEWalletProvider : IEWalletProvider
{
    private readonly ILogger<MockEWalletProvider> _logger;

    public MockEWalletProvider(ILogger<MockEWalletProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "MockEWallet";

    // ── Link ────────────────────────────────────────────────────────────────

    public async Task<EWalletLinkResult> InitiateLinkingAsync(string walletAccountId, string customerName)
    {
        await Task.Delay(200); // simulate network

        _logger.LogInformation("[MOCK-EWALLET] InitiateLinking: WalletAccountId={WalletAccountId} CustomerName={Name}",
            walletAccountId, customerName);

        // Simulate failure
        if (walletAccountId.Equals("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[MOCK-EWALLET] Simulating FAILURE for WalletAccountId=FAIL");
            return new EWalletLinkResult
            {
                IsSuccess = false,
                ErrorMessage = "MOCK: Tài khoản ví không tồn tại hoặc không hợp lệ."
            };
        }

        var otpNonce = $"OTP-{Guid.NewGuid():N}";
        _logger.LogInformation("[MOCK-EWALLET] OTP sent to wallet {WalletAccountId}. Nonce={Nonce}. Use OTP '123456' to verify.",
            walletAccountId, otpNonce);

        return new EWalletLinkResult
        {
            IsSuccess = true,
            OtpNonce = otpNonce,
            WalletUserName = $"Ví {walletAccountId.Substring(0, Math.Min(4, walletAccountId.Length))}***"
        };
    }

    // ── Verify OTP ──────────────────────────────────────────────────────────

    public async Task<EWalletVerifyResult> VerifyOtpAsync(string otpNonce, string otpCode)
    {
        await Task.Delay(150); // simulate network

        _logger.LogInformation("[MOCK-EWALLET] VerifyOtp: Nonce={Nonce} OtpCode={Code}", otpNonce, otpCode);

        if (otpCode != "123456")
        {
            return new EWalletVerifyResult
            {
                IsSuccess = false,
                ErrorMessage = "OTP không đúng. Vui lòng kiểm tra lại mã OTP từ ví."
            };
        }

        var walletToken = $"WTOKEN-{Guid.NewGuid():N}";
        _logger.LogInformation("[MOCK-EWALLET] OTP verified. WalletToken={Token}", walletToken);

        return new EWalletVerifyResult
        {
            IsSuccess = true,
            WalletToken = walletToken,
            WalletUserName = "Nguyễn Văn Mock"
        };
    }

    // ── Charge (thanh toán) ─────────────────────────────────────────────────

    public async Task<EWalletChargeResult> ChargeAsync(string walletToken, decimal amount, string description)
    {
        await Task.Delay(300); // simulate network

        var txId = $"EWALLET-CHG-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(10000, 99999)}";

        _logger.LogInformation("[MOCK-EWALLET] Charge: Token={Token} Amount={Amount:N0} Desc={Desc} TxId={TxId}",
            walletToken, amount, description, txId);

        // Simulate insufficient balance
        if (walletToken.Contains("INSUFFICIENT", StringComparison.OrdinalIgnoreCase))
        {
            return new EWalletChargeResult
            {
                IsSuccess = false,
                TransactionId = txId,
                Status = "FAILED",
                Amount = amount,
                ProcessedAt = DateTime.UtcNow,
                ErrorCode = "INSUFFICIENT_BALANCE",
                ErrorMessage = "Số dư ví không đủ để thực hiện giao dịch."
            };
        }

        return new EWalletChargeResult
        {
            IsSuccess = true,
            TransactionId = txId,
            Status = "SUCCESS",
            Amount = amount,
            ProcessedAt = DateTime.UtcNow
        };
    }

    // ── Payout (giải ngân) ──────────────────────────────────────────────────

    public async Task<EWalletChargeResult> PayoutAsync(string walletToken, decimal amount, string description)
    {
        await Task.Delay(300); // simulate network

        var txId = $"EWALLET-PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(10000, 99999)}";

        _logger.LogInformation("[MOCK-EWALLET] Payout: Token={Token} Amount={Amount:N0} Desc={Desc} TxId={TxId}",
            walletToken, amount, description, txId);

        return new EWalletChargeResult
        {
            IsSuccess = true,
            TransactionId = txId,
            Status = "SUCCESS",
            Amount = amount,
            ProcessedAt = DateTime.UtcNow
        };
    }

    // ── Query ───────────────────────────────────────────────────────────────

    public async Task<EWalletChargeResult> QueryTransactionAsync(string transactionId)
    {
        await Task.Delay(100);

        _logger.LogInformation("[MOCK-EWALLET] QueryTransaction: TxId={TxId}", transactionId);

        return new EWalletChargeResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            Status = "SUCCESS",
            Amount = 0,
            ProcessedAt = DateTime.UtcNow
        };
    }
}
