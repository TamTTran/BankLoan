using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanFlow.Application.Services;
using Microsoft.Extensions.Options;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// VNPay E-Wallet Provider — production HTTP implementation.
///
/// Authentication: HMAC-SHA512 signature theo VNPay API v2.1.0 spec.
/// Signature string format: sort all params alphabetically → concat key=value&... → HMAC-SHA512 với HashSecret.
/// Signature result: vnp_SecureHash (lowercase hex).
///
/// Tài liệu: https://sandbox.vnpayment.vn/apis/
/// Sandbox:  https://sandbox.vnpayment.vn
/// </summary>
public class VNPayEWalletProvider : IEWalletProvider
{
    private readonly HttpClient _http;
    private readonly VNPayOptions _opts;
    private readonly ILogger<VNPayEWalletProvider> _logger;

    public string ProviderName => "VNPay";

    public VNPayEWalletProvider(
        HttpClient http,
        IOptions<VNPayOptions> opts,
        ILogger<VNPayEWalletProvider> logger)
    {
        _http   = http;
        _opts   = opts.Value;
        _logger = logger;
    }

    // ── HMAC-SHA512 Helper ───────────────────────────────────────────────────

    /// <summary>
    /// Tạo chữ ký HMAC-SHA512 theo VNPay spec.
    /// params: dictionary key-value — tự động sort alphabetically trước khi ký.
    /// </summary>
    private string Sign(SortedDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in parameters)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append($"{key}={value}");
        }

        var rawData  = sb.ToString();
        var keyBytes = Encoding.UTF8.GetBytes(_opts.HashSecret);
        var msgBytes = Encoding.UTF8.GetBytes(rawData);

        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(msgBytes)).ToLowerInvariant();
    }

    // ── Initiate Linking ─────────────────────────────────────────────────────

    /// <summary>
    /// Gửi yêu cầu liên kết ví VNPay → VNPay gửi OTP cho khách hàng.
    /// Endpoint: POST /ewallet/api/link/init
    /// </summary>
    public async Task<EWalletLinkResult> InitiateLinkingAsync(string walletAccountId, string customerName)
    {
        var txnRef    = $"LINK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N[..6]}";
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]     = _opts.ApiVersion,
            ["vnp_Command"]     = "link_wallet",
            ["vnp_TmnCode"]     = _opts.TmnCode,
            ["vnp_TxnRef"]      = txnRef,
            ["vnp_OrderInfo"]   = $"Lien ket vi VNPay - {customerName}",
            ["vnp_WalletPhone"] = walletAccountId,
            ["vnp_CreateDate"]  = createDate,
            ["vnp_ReturnUrl"]   = _opts.ReturnUrl,
        };

        var signature = Sign(parameters);
        parameters["vnp_SecureHash"] = signature;

        try
        {
            _logger.LogInformation("[VNPAY] InitiateLinking TxnRef={TxnRef} Phone={Phone}",
                txnRef, walletAccountId);

            var response = await _http.PostAsJsonAsync("/ewallet/api/link/init", parameters);
            var body     = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("[VNPAY] InitiateLinking response: {Body}", body);

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var responseCode = root.TryGetProperty("vnp_ResponseCode", out var rc)
                               ? rc.GetString() : "99";

            if (responseCode != "00")
            {
                var msg = root.TryGetProperty("vnp_Message", out var m) ? m.GetString() : "Lỗi VNPay";
                _logger.LogWarning("[VNPAY] InitiateLinking failed. Code={Code} Msg={Msg}", responseCode, msg);
                return new EWalletLinkResult { IsSuccess = false, ErrorMessage = $"VNPay: {msg}" };
            }

            return new EWalletLinkResult
            {
                IsSuccess      = true,
                OtpNonce       = txnRef,
                WalletUserName = $"Ví VNPay ({walletAccountId.Substring(0, Math.Min(4, walletAccountId.Length))}***)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VNPAY] InitiateLinking threw exception for {Phone}", walletAccountId);
            return new EWalletLinkResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    // ── Verify OTP ───────────────────────────────────────────────────────────

    /// <summary>
    /// Xác thực OTP từ ứng dụng VNPay → nhận wallet binding token.
    /// Endpoint: POST /ewallet/api/link/confirm
    /// </summary>
    public async Task<EWalletVerifyResult> VerifyOtpAsync(string otpNonce, string otpCode)
    {
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]    = _opts.ApiVersion,
            ["vnp_Command"]    = "link_wallet_confirm",
            ["vnp_TmnCode"]    = _opts.TmnCode,
            ["vnp_TxnRef"]     = otpNonce,
            ["vnp_Otp"]        = otpCode,
            ["vnp_CreateDate"] = createDate,
        };

        var signature = Sign(parameters);
        parameters["vnp_SecureHash"] = signature;

        try
        {
            _logger.LogInformation("[VNPAY] VerifyOtp TxnRef={TxnRef}", otpNonce);

            var response = await _http.PostAsJsonAsync("/ewallet/api/link/confirm", parameters);
            var body     = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var responseCode = root.TryGetProperty("vnp_ResponseCode", out var rc) ? rc.GetString() : "99";
            if (responseCode != "00")
            {
                var msg = root.TryGetProperty("vnp_Message", out var m) ? m.GetString() : "OTP không hợp lệ";
                return new EWalletVerifyResult { IsSuccess = false, ErrorMessage = $"VNPay: {msg}" };
            }

            var walletToken = root.TryGetProperty("vnp_WalletToken", out var wt) ? wt.GetString() : otpNonce;
            var displayName = root.TryGetProperty("vnp_WalletAlias", out var wa) ? wa.GetString() : null;

            return new EWalletVerifyResult
            {
                IsSuccess      = true,
                WalletToken    = walletToken,
                WalletUserName = displayName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VNPAY] VerifyOtp threw exception for TxnRef={TxnRef}", otpNonce);
            return new EWalletVerifyResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    // ── Charge (Thanh toán) ──────────────────────────────────────────────────

    /// <summary>
    /// Trừ tiền từ ví VNPay (thanh toán khoản vay).
    /// Endpoint: POST /ewallet/api/pay
    /// Amount: VNPay tính theo đơn vị VND × 100 (ví dụ: 50000 VND → vnp_Amount=5000000).
    /// </summary>
    public async Task<EWalletChargeResult> ChargeAsync(string walletToken, decimal amount, string description)
    {
        var txnRef     = $"CHG-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N[..6]}";
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
        var amountVnp  = ((long)amount * 100).ToString(); // VNPay dùng VND × 100

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]     = _opts.ApiVersion,
            ["vnp_Command"]     = "pay",
            ["vnp_TmnCode"]     = _opts.TmnCode,
            ["vnp_Amount"]      = amountVnp,
            ["vnp_TxnRef"]      = txnRef,
            ["vnp_OrderInfo"]   = description,
            ["vnp_WalletToken"] = walletToken,
            ["vnp_CreateDate"]  = createDate,
            ["vnp_ReturnUrl"]   = _opts.ReturnUrl,
        };

        var signature = Sign(parameters);
        parameters["vnp_SecureHash"] = signature;

        return await ExecutePaymentAsync("/ewallet/api/pay", parameters, txnRef, amount, "CHG");
    }

    // ── Payout (Giải ngân) ───────────────────────────────────────────────────

    /// <summary>
    /// Chuyển tiền vào ví VNPay (giải ngân).
    /// Endpoint: POST /ewallet/api/transfer
    /// </summary>
    public async Task<EWalletChargeResult> PayoutAsync(string walletToken, decimal amount, string description)
    {
        var txnRef     = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N[..6]}";
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
        var amountVnp  = ((long)amount * 100).ToString();

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]     = _opts.ApiVersion,
            ["vnp_Command"]     = "transfer",
            ["vnp_TmnCode"]     = _opts.TmnCode,
            ["vnp_Amount"]      = amountVnp,
            ["vnp_TxnRef"]      = txnRef,
            ["vnp_OrderInfo"]   = description,
            ["vnp_WalletToken"] = walletToken,
            ["vnp_CreateDate"]  = createDate,
        };

        var signature = Sign(parameters);
        parameters["vnp_SecureHash"] = signature;

        return await ExecutePaymentAsync("/ewallet/api/transfer", parameters, txnRef, amount, "PAY");
    }

    // ── Query Transaction ────────────────────────────────────────────────────

    /// <summary>
    /// Truy vấn trạng thái giao dịch VNPay.
    /// Endpoint: POST /ewallet/api/query
    /// </summary>
    public async Task<EWalletChargeResult> QueryTransactionAsync(string transactionId)
    {
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]    = _opts.ApiVersion,
            ["vnp_Command"]    = "querydr",
            ["vnp_TmnCode"]    = _opts.TmnCode,
            ["vnp_TxnRef"]     = transactionId,
            ["vnp_CreateDate"] = createDate,
        };

        var signature = Sign(parameters);
        parameters["vnp_SecureHash"] = signature;

        try
        {
            _logger.LogInformation("[VNPAY] QueryTransaction TxId={TxId}", transactionId);

            var response = await _http.PostAsJsonAsync("/ewallet/api/query", parameters);
            var body     = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var responseCode = root.TryGetProperty("vnp_ResponseCode", out var rc) ? rc.GetString() : "99";
            var status       = responseCode == "00" ? "SUCCESS" : "FAILED";
            var msg          = root.TryGetProperty("vnp_Message", out var m) ? m.GetString() : null;
            var amountStr    = root.TryGetProperty("vnp_Amount", out var a) ? a.GetString() : "0";
            var amount       = long.TryParse(amountStr, out var al) ? al / 100m : 0m;

            return new EWalletChargeResult
            {
                IsSuccess     = responseCode == "00",
                TransactionId = transactionId,
                Status        = status,
                Amount        = amount,
                ProcessedAt   = DateTime.UtcNow,
                ErrorCode     = responseCode != "00" ? responseCode : null,
                ErrorMessage  = responseCode != "00" ? msg : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VNPAY] QueryTransaction threw exception for {TxId}", transactionId);
            return new EWalletChargeResult
            {
                IsSuccess     = false,
                TransactionId = transactionId,
                Status        = "ERROR",
                ProcessedAt   = DateTime.UtcNow,
                ErrorMessage  = ex.Message
            };
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private async Task<EWalletChargeResult> ExecutePaymentAsync(
        string endpoint,
        SortedDictionary<string, string> parameters,
        string txnRef,
        decimal amount,
        string prefix)
    {
        try
        {
            _logger.LogInformation("[VNPAY] {Prefix} TxnRef={TxnRef} Amount={Amount:N0}",
                prefix, txnRef, amount);

            var response = await _http.PostAsJsonAsync(endpoint, parameters);
            var body     = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("[VNPAY] {Prefix} response: {Body}", prefix, body);

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var responseCode = root.TryGetProperty("vnp_ResponseCode", out var rc) ? rc.GetString() : "99";
            var vnpTxnId     = root.TryGetProperty("vnp_TransactionNo", out var tn) ? tn.GetString() : txnRef;
            var status       = responseCode == "00" ? "SUCCESS" : "FAILED";
            var msg          = root.TryGetProperty("vnp_Message", out var m) ? m.GetString() : null;

            return new EWalletChargeResult
            {
                IsSuccess     = responseCode == "00",
                TransactionId = vnpTxnId ?? txnRef,
                Status        = status,
                Amount        = amount,
                ProcessedAt   = DateTime.UtcNow,
                ErrorCode     = responseCode != "00" ? responseCode : null,
                ErrorMessage  = responseCode != "00" ? msg : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VNPAY] {Prefix} threw exception TxnRef={TxnRef}", prefix, txnRef);
            return new EWalletChargeResult
            {
                IsSuccess     = false,
                TransactionId = txnRef,
                Status        = "ERROR",
                Amount        = amount,
                ProcessedAt   = DateTime.UtcNow,
                ErrorMessage  = ex.Message
            };
        }
    }
}
