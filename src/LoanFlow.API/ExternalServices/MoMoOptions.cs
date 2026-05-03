namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Strongly-typed configuration for MoMo E-Wallet API integration.
/// Section: ExternalServices:MoMo
///
/// MoMo API: https://developers.momo.vn/
/// Signature: HMAC-SHA256 — key-ordered param string + SecretKey
/// </summary>
public class MoMoOptions
{
    public const string SectionName = "ExternalServices:MoMo";

    /// <summary>Base URL: https://test-payment.momo.vn (sandbox) | https://payment.momo.vn (prod)</summary>
    public string ApiUrl { get; set; } = "https://test-payment.momo.vn";

    /// <summary>Mã đối tác (Partner Code) do MoMo cấp sau khi đăng ký Business Account.</summary>
    public string PartnerCode { get; set; } = string.Empty;

    /// <summary>Access Key do MoMo cấp — dùng trong chuỗi ký HMAC-SHA256.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Secret Key — bí mật, dùng để ký HMAC-SHA256. KHÔNG được commit vào source code.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Timeout mỗi request HTTP (giây). Mặc định 30s.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Số lần retry khi gặp transient error. Mặc định 2.</summary>
    public int MaxRetryAttempts { get; set; } = 2;
}
