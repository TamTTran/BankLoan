using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanFlow.Application.Services;
using Microsoft.Extensions.Options;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// MoMo E-Wallet Provider — production HTTP implementation.
///
/// Authentication: HMAC-SHA256 signature theo MoMo API v2 spec.
/// Signature string format (thứ tự cố định theo tài liệu MoMo):
///   accessKey=...&amount=...&description=...&...&requestId=...&secretKey=...
///
/// Tài liệu: https://developers.momo.vn/
/// Môi trường sandbox: https://test-payment.momo.vn
/// </summary>
public class MoMoEWalletProvider : IEWalletProvider
{
    private readonly HttpClient _http;
    private readonly MoMoOptions _opts;
    private readonly ILogger<MoMoEWalletProvider> _logger;

    public string ProviderName => "MoMo";

    public MoMoEWalletProvider(
        HttpClient http,
        IOptions<MoMoOptions> opts,
        ILogger<MoMoEWalletProvider> logger)
    {
        _http   = http;
        _opts   = opts.Value;
        _logger = logger;
    }

    // ── HMAC-SHA256 Helper ───────────────────────────────────────────────────

    /// <summary>
    /// Tạo chữ ký HMAC-SHA256 theo MoMo spec.
    /// rawData: chuỗi ghép theo thứ tự cố định của từng API endpoint.
    /// </summary>
    private string Sign(string rawData)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(_opts.SecretKey);
        var dataBytes = Encoding.UTF8.GetBytes(rawData);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
    }

    // ── Initiate Linking ─────────────────────────────────────────────────────

    /// <summary>
    /// Gửi yêu cầu liên kết ví → MoMo gửi OTP đến số điện thoại của khách hàng.
    /// Endpoint: POST /v2/gateway/api/create
    /// </summary>
    public async Task<EWalletLinkResult> InitiateLinkingAsync(string walletAccountId, string customerName)
    {
        var requestId = $"LINK-{Guid.NewGuid():N}";
        var orderId   = requestId;
        var amount    = 0L; // Liên kết ví không thu tiền — amount = 0
        var orderInfo = $"Liên kết ví MoMo — {customerName}";
        var extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(walletAccountId));

        // Signature string: thứ tự field theo MoMo AIO v2
        var rawSignature =
            $"accessKey={_opts.AccessKey}" +
            $"&amount={amount}" +
            $"&extraData={extraData}" +
            $"&ipnUrl={string.Empty}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&partnerCode={_opts.PartnerCode}" +
            $"&redirectUrl={string.Empty}" +
            $"&requestId={requestId}" +
            $"&requestType=linkWallet";

        var signature = Sign(rawSignature);

        var payload = new
        {
            partnerCode = _opts.PartnerCode,
            partnerName = "LoanFlow",
            storeId     = _opts.PartnerCode,
            requestId,
            amount,
            orderId,
            orderInfo,
            redirectUrl = string.Empty,
            ipnUrl      = string.Empty,
            lang        = "vi",
            extraData,
            requestType = "linkWallet",
            signature
        };

        try
        {
            _logger.LogInformation("[MOMO] InitiateLinking RequestId={RequestId} Phone={Phone}",
                requestId, walletAccountId);

            var response = await _http.PostAsJsonAsync("/v2/gateway/api/create", payload);
            var body     = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("[MOMO] InitiateLinking response: {Body}", body);

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var resultCode = root.GetProperty("resultCode").GetInt32();
            if (resultCode != 0)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Lỗi MoMo không xác định";
                _logger.LogWarning("[MOMO] InitiateLinking failed. ResultCode={Code} Message={Msg}", resultCode, msg);
                return new EWalletLinkResult { IsSuccess = false, ErrorMessage = $"MoMo: {msg}" };
            }

            // MoMo trả lại paymentUrl (deeplink) để redirect user, hoặc OTP qua ứng dụng MoMo
            var otpNonce = root.TryGetProperty("payToken", out var t) ? t.GetString() : requestId;

            return new EWalletLinkResult
            {
                IsSuccess    = true,
                OtpNonce     = otpNonce,
                WalletUserName = $"Ví MoMo ({walletAccountId.Substring(0, Math.Min(4, walletAccountId.Length))}***)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MOMO] InitiateLinking threw exception for {Phone}", walletAccountId);
            return new EWalletLinkResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    // ── Verify OTP ───────────────────────────────────────────────────────────

    /// <summary>
    /// Xác thực OTP từ ứng dụng MoMo → nhận wallet binding token.
    /// Endpoint: POST /v2/gateway/api/confirm
    /// </summary>
    public async Task<EWalletVerifyResult> VerifyOtpAsync(string otpNonce, string otpCode)
    {
        var requestId = $"VERIFY-{Guid.NewGuid():N}";

        // MoMo dùng payToken (otpNonce) kết hợp OTP để confirm
        var rawSignature =
            $"accessKey={_opts.AccessKey}" +
            $"&orderId={otpNonce}" +
            $"&partnerCode={_opts.PartnerCode}" +
            $"&requestId={requestId}" +
            $"&requestType=confirm";

        var signature = Sign(rawSignature);

        var payload = new
        {
            partnerCode = _opts.PartnerCode,
            requestId,
            orderId     = otpNonce,
            otp         = otpCode,
            lang        = "vi",
            requestType = "confirm",
            signature
        };

        try
        {
            _logger.LogInformation("[MOMO] VerifyOtp RequestId={RequestId}", requestId);

            var response = await _http.PostAsJsonAsync("/v2/gateway/api/confirm", payload);
            var body     = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var resultCode = root.GetProperty("resultCode").GetInt32();
            if (resultCode != 0)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "OTP không hợp lệ";
                return new EWalletVerifyResult { IsSuccess = false, ErrorMessage = $"MoMo: {msg}" };
            }

            var walletToken = root.TryGetProperty("payToken", out var wt) ? wt.GetString() : null;
            var displayName = root.TryGetProperty("phoneNo",  out var ph) ? ph.GetString() : null;

            return new EWalletVerifyResult
            {
                IsSuccess      = true,
                WalletToken    = walletToken,
                WalletUserName = displayName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MOMO] VerifyOtp threw exception");
            return new EWalletVerifyResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    // ── Charge (Thanh toán) ──────────────────────────────────────────────────

    /// <summary>
    /// Trừ tiền từ ví MoMo (thanh toán khoản vay).
    /// Endpoint: POST /v2/gateway/api/pay
    /// </summary>
    public async Task<EWalletChargeResult> ChargeAsync(string walletToken, decimal amount, string description)
    {
        var requestId = $"CHG-{Guid.NewGuid():N}";
        var orderId   = requestId;
        var amountLong = (long)amount;

        var rawSignature =
            $"accessKey={_opts.AccessKey}" +
            $"&amount={amountLong}" +
            $"&description={description}" +
            $"&orderId={orderId}" +
            $"&orderInfo={description}" +
            $"&partnerCode={_opts.PartnerCode}" +
            $"&payToken={walletToken}" +
            $"&requestId={requestId}" +
            $"&requestType=pay";

        var signature = Sign(rawSignature);

        var payload = new
        {
            partnerCode = _opts.PartnerCode,
            requestId,
            orderId,
            amount      = amountLong,
            orderInfo   = description,
            payToken    = walletToken,
            lang        = "vi",
            requestType = "pay",
            signature
        };

        return await ExecuteChargeAsync("/v2/gateway/api/pay", payload, requestId, amount, "CHG");
    }

    // ── Payout (Giải ngân) ───────────────────────────────────────────────────

    /// <summary>
    /// Chuyển tiền vào ví MoMo (giải ngân).
    /// Endpoint: POST /v2/gateway/api/disbursement
    /// </summary>
    public async Task<EWalletChargeResult> PayoutAsync(string walletToken, decimal amount, string description)
    {
        var requestId  = $"PAY-{Guid.NewGuid():N}";
        var orderId    = requestId;
        var amountLong = (long)amount;

        var rawSignature =
            $"accessKey={_opts.AccessKey}" +
            $"&amount={amountLong}" +
            $"&description={description}" +
            $"&orderId={orderId}" +
            $"&orderInfo={description}" +
            $"&partnerCode={_opts.PartnerCode}" +
            $"&payToken={walletToken}" +
            $"&requestId={requestId}" +
            $"&requestType=disbursement";

        var signature = Sign(rawSignature);

        var payload = new
        {
            partnerCode = _opts.PartnerCode,
            requestId,
            orderId,
            amount      = amountLong,
            orderInfo   = description,
            payToken    = walletToken,
            lang        = "vi",
            requestType = "disbursement",
            signature
        };

        return await ExecuteChargeAsync("/v2/gateway/api/disbursement", payload, requestId, amount, "PAY");
    }

    // ── Query Transaction ────────────────────────────────────────────────────

    /// <summary>
    /// Truy vấn trạng thái giao dịch MoMo.
    /// Endpoint: POST /v2/gateway/api/query
    /// </summary>
    public async Task<EWalletChargeResult> QueryTransactionAsync(string transactionId)
    {
        var requestId = $"QRY-{Guid.NewGuid():N}";

        var rawSignature =
            $"accessKey={_opts.AccessKey}" +
            $"&orderId={transactionId}" +
            $"&partnerCode={_opts.PartnerCode}" +
            $"&requestId={requestId}";

        var signature = Sign(rawSignature);

        var payload = new
        {
            partnerCode = _opts.PartnerCode,
            requestId,
            orderId     = transactionId,
            lang        = "vi",
            signature
        };

        try
        {
            _logger.LogInformation("[MOMO] QueryTransaction TxId={TxId}", transactionId);

            var response = await _http.PostAsJsonAsync("/v2/gateway/api/query", payload);
            var body     = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var resultCode = root.GetProperty("resultCode").GetInt32();
            var status     = resultCode == 0 ? "SUCCESS" : "FAILED";
            var msg        = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            var amount     = root.TryGetProperty("amount",  out var a) ? (decimal)a.GetInt64() : 0m;

            return new EWalletChargeResult
            {
                IsSuccess      = resultCode == 0,
                TransactionId  = transactionId,
                Status         = status,
                Amount         = amount,
                ProcessedAt    = DateTime.UtcNow,
                ErrorCode      = resultCode != 0 ? resultCode.ToString() : null,
                ErrorMessage   = resultCode != 0 ? msg : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MOMO] QueryTransaction threw exception for {TxId}", transactionId);
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

    private async Task<EWalletChargeResult> ExecuteChargeAsync(
        string endpoint, object payload, string requestId, decimal amount, string prefix)
    {
        try
        {
            _logger.LogInformation("[MOMO] {Prefix} RequestId={RequestId} Amount={Amount:N0}",
                prefix, requestId, amount);

            var response = await _http.PostAsJsonAsync(endpoint, payload);
            var body     = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("[MOMO] {Prefix} response: {Body}", prefix, body);

            using var doc = JsonDocument.Parse(body);
            var root      = doc.RootElement;

            var resultCode = root.GetProperty("resultCode").GetInt32();
            var txId       = root.TryGetProperty("transId", out var tx)
                             ? tx.GetInt64().ToString()
                             : requestId;
            var status     = resultCode == 0 ? "SUCCESS" : "FAILED";
            var msg        = root.TryGetProperty("message", out var m) ? m.GetString() : null;

            return new EWalletChargeResult
            {
                IsSuccess     = resultCode == 0,
                TransactionId = txId,
                Status        = status,
                Amount        = amount,
                ProcessedAt   = DateTime.UtcNow,
                ErrorCode     = resultCode != 0 ? resultCode.ToString() : null,
                ErrorMessage  = resultCode != 0 ? msg : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MOMO] {Prefix} threw exception RequestId={RequestId}", prefix, requestId);
            return new EWalletChargeResult
            {
                IsSuccess     = false,
                TransactionId = requestId,
                Status        = "ERROR",
                Amount        = amount,
                ProcessedAt   = DateTime.UtcNow,
                ErrorMessage  = ex.Message
            };
        }
    }
}
