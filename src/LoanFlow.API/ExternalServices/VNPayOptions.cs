namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Strongly-typed configuration for VNPay E-Wallet API integration.
/// Section: ExternalServices:VNPay
///
/// VNPay API: https://sandbox.vnpayment.vn/apis/
/// Signature: HMAC-SHA512 — params sorted alphabetically, concat key=value, hash with HashSecret
/// </summary>
public class VNPayOptions
{
    public const string SectionName = "ExternalServices:VNPay";

    /// <summary>Base URL: https://sandbox.vnpayment.vn (sandbox) | https://pay.vnpay.vn (prod)</summary>
    public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn";

    /// <summary>Mã Terminal (TmnCode) do VNPay cấp.</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>Hash Secret Key — dùng để ký HMAC-SHA512. KHÔNG commit vào source code.</summary>
    public string HashSecret { get; set; } = string.Empty;

    /// <summary>Return/IPN URL — URL nhận callback từ VNPay sau khi giao dịch hoàn tất.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>Phiên bản API VNPay. Mặc định "2.1.0".</summary>
    public string ApiVersion { get; set; } = "2.1.0";

    /// <summary>Timeout mỗi request HTTP (giây). Mặc định 30s.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Số lần retry khi gặp transient error. Mặc định 2.</summary>
    public int MaxRetryAttempts { get; set; } = 2;
}
