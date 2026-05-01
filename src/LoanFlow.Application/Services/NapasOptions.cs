namespace LoanFlow.Application.Services;

/// <summary>
/// Strongly-typed configuration cho NAPAS Payment Gateway.
/// Bind từ section "ExternalServices:Napas" trong appsettings / KeyVault.
/// </summary>
public class NapasOptions
{
    public const string SectionName = "ExternalServices:Napas";

    /// <summary>Base URL của NAPAS API (sandbox hoặc production)</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>API Key / Bearer token để xác thực với NAPAS</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret để verify chữ ký HMAC-SHA256 trên callback từ NAPAS.
    /// Phải trùng với secret đã đăng ký trên NAPAS Merchant Portal.
    /// </summary>
    public string CallbackSecret { get; set; } = string.Empty;

    /// <summary>Timeout HTTP tính bằng giây (default: 10s)</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Số lần retry khi gặp lỗi tạm thời 5xx / timeout (default: 3)</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Thời gian chờ giữa các lần retry, tính bằng milliseconds (default: 1000ms, exponential backoff)</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Tên môi trường NAPAS để ghi log / trace.
    /// Ví dụ: "sandbox" | "production"
    /// </summary>
    public string Environment { get; set; } = "sandbox";
}
