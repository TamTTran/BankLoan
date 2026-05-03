namespace LoanFlow.Application.Services;

/// <summary>
/// Provider abstraction cho ví điện tử (MoMo, VNPay, ...).
/// Pattern giống IEkycProvider, ICicProvider — mock trong dev, real trong prod.
/// </summary>
public interface IEWalletProvider
{
    string ProviderName { get; }

    /// <summary>Gửi yêu cầu liên kết ví → ví gửi OTP cho user</summary>
    Task<EWalletLinkResult> InitiateLinkingAsync(string walletAccountId, string customerName);

    /// <summary>Xác thực OTP → trả token binding</summary>
    Task<EWalletVerifyResult> VerifyOtpAsync(string otpNonce, string otpCode);

    /// <summary>Trừ tiền từ ví (thanh toán khoản vay)</summary>
    Task<EWalletChargeResult> ChargeAsync(string walletToken, decimal amount, string description);

    /// <summary>Chuyển tiền vào ví (giải ngân)</summary>
    Task<EWalletChargeResult> PayoutAsync(string walletToken, decimal amount, string description);

    /// <summary>Query trạng thái giao dịch</summary>
    Task<EWalletChargeResult> QueryTransactionAsync(string transactionId);
}

// ── Provider DTOs ───────────────────────────────────────────────────────────

public class EWalletLinkResult
{
    public bool IsSuccess { get; set; }
    public string? OtpNonce { get; set; }
    public string? WalletUserName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EWalletVerifyResult
{
    public bool IsSuccess { get; set; }
    public string? WalletToken { get; set; }
    public string? WalletUserName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EWalletChargeResult
{
    public bool IsSuccess { get; set; }
    public string TransactionId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
