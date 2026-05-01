using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Production NAPAS Payment Gateway integration.
///
/// Cấu hình qua section "ExternalServices:Napas" (hoặc Azure Key Vault):
///   - ApiUrl         : NAPAS API endpoint (sandbox / production)
///   - ApiKey         : Bearer token
///   - CallbackSecret : HMAC-SHA256 secret để verify callback từ NAPAS
///   - TimeoutSeconds : HTTP timeout (default 10s)
///   - MaxRetryAttempts / RetryBaseDelayMs : Polly retry
///
/// Polly resilience pipeline được inject qua HttpClient (xem Program.cs):
///   - Retry 3 lần với exponential backoff khi gặp 5xx / timeout
///   - Total timeout 30s bao gồm tất cả retry
/// </summary>
public class NapasPaymentGatewayService : IPaymentGatewayService
{
    private readonly HttpClient _httpClient;
    private readonly LoanFlowDbContext _db;
    private readonly NapasOptions _options;
    private readonly ILogger<NapasPaymentGatewayService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public NapasPaymentGatewayService(
        HttpClient httpClient,
        LoanFlowDbContext db,
        IOptions<NapasOptions> options,
        ILogger<NapasPaymentGatewayService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
        _logger = logger;

        // Set base address once on construction (safe — HttpClient is per-request keyed)
        if (!string.IsNullOrWhiteSpace(_options.ApiUrl))
            _httpClient.BaseAddress = new Uri(_options.ApiUrl.TrimEnd('/') + "/");
    }

    // ── [SYNC] TransferToCustomerAsync ──────────────────────────────────────

    public async Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(
        string bankCode, string accountNumber, decimal amount, string remark)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<PaymentGatewayResponse>("Bank account number is required.");
        if (string.IsNullOrWhiteSpace(bankCode))
            return Result.Failure<PaymentGatewayResponse>("Bank code is required.");
        if (amount <= 0)
            return Result.Failure<PaymentGatewayResponse>("Transfer amount must be greater than zero.");
        if (!NapasBankCode.IsValid(bankCode))
            return Result.Failure<PaymentGatewayResponse>($"'{bankCode}' is not a valid NAPAS member bank code.");

        var referenceId = GenerateReferenceId();

        // NAPAS Transfer Request payload (theo chuẩn NAPAS Pay 24/7 API spec)
        var payload = new
        {
            referenceId,
            beneficiary = new
            {
                bankCode,
                accountNumber,
                accountName = (string?)null  // optional — NAPAS validates internally
            },
            amount         = (long)(amount * 100), // NAPAS dùng đơn vị xu (VND x 100)
            currency       = "VND",
            remark         = remark.Length > 140 ? remark[..140] : remark,  // NAPAS max 140 chars
            transferType   = "IBFT",  // Interbank Fund Transfer
            requestedAt    = DateTime.UtcNow.ToString("o")
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/transfers");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "[NAPAS] Sending transfer. ReferenceId={ReferenceId} BankCode={BankCode} Amount={Amount:N0}",
                referenceId, bankCode, amount);

            // Polly retry pipeline xử lý qua DI (AddResilienceHandler trong Program.cs)
            using var response = await _httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("[NAPAS] Transfer response {StatusCode}: {Body}", (int)response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[NAPAS] Transfer failed. ReferenceId={ReferenceId} HTTP={StatusCode} Body={Body}",
                    referenceId, (int)response.StatusCode, responseBody);
                return Result.Failure<PaymentGatewayResponse>(
                    $"NAPAS gateway error: HTTP {(int)response.StatusCode}. ReferenceId: {referenceId}");
            }

            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody, _jsonOptions);

            // Đọc status từ NAPAS response
            var napasStatus = responseJson.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? string.Empty
                : string.Empty;

            // NAPAS có thể trả SUCCESS ngay (sync) hoặc PENDING (async)
            var napasTransactionId = responseJson.TryGetProperty("transactionId", out var txEl)
                ? txEl.GetString() ?? string.Empty
                : string.Empty;

            var isConfirmedImmediately = napasStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);

            if (napasStatus.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var errorCode = responseJson.TryGetProperty("errorCode", out var errEl) ? errEl.GetString() : null;
                var errorMsg  = responseJson.TryGetProperty("errorMessage", out var errMsgEl) ? errMsgEl.GetString() : null;
                _logger.LogWarning("[NAPAS] Transfer FAILED. ReferenceId={ReferenceId} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                    referenceId, errorCode, errorMsg);
                return Result.Failure<PaymentGatewayResponse>(
                    $"NAPAS rejected transfer: [{errorCode}] {errorMsg}. ReferenceId: {referenceId}");
            }

            _logger.LogInformation(
                "[NAPAS] Transfer {Status}. ReferenceId={ReferenceId} NapasTxId={TxId} Confirmed={Confirmed}",
                napasStatus, referenceId, napasTransactionId, isConfirmedImmediately);

            return Result.Success(new PaymentGatewayResponse
            {
                TransactionId          = napasTransactionId,
                ReferenceId            = referenceId,
                ProcessedAt            = DateTime.UtcNow,
                OriginalBankCode       = bankCode,
                OriginalAccountNumber  = accountNumber,
                IsConfirmedImmediately = isConfirmedImmediately
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[NAPAS] Network error. ReferenceId={ReferenceId}", referenceId);
            return Result.Failure<PaymentGatewayResponse>(
                $"NAPAS unavailable (network): {ex.Message}. ReferenceId: {referenceId}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[NAPAS] Timeout after all retries. ReferenceId={ReferenceId}", referenceId);
            return Result.Failure<PaymentGatewayResponse>(
                $"NAPAS timeout after retries. ReferenceId: {referenceId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NAPAS] Unexpected error. ReferenceId={ReferenceId}", referenceId);
            return Result.Failure<PaymentGatewayResponse>(
                $"NAPAS unexpected error: {ex.Message}. ReferenceId: {referenceId}");
        }
    }

    // ── [ASYNC CALLBACK] HandleCallbackAsync ────────────────────────────────

    public async Task<Result<CallbackHandleResult>> HandleCallbackAsync(NapasCallbackPayload payload)
    {
        _logger.LogInformation(
            "[NAPAS] HandleCallback: ReferenceId={ReferenceId} Status={Status} NapasTxId={NapasTxId}",
            payload.ReferenceId, payload.Status, payload.NapasTransactionId);

        var disbursement = await _db.LoanDisbursements
            .FirstOrDefaultAsync(d => d.NapasReferenceId == payload.ReferenceId);

        if (disbursement is null)
        {
            _logger.LogWarning("[NAPAS] Callback received but no disbursement found for ReferenceId={ReferenceId}", payload.ReferenceId);
            return Result.Failure<CallbackHandleResult>(
                $"No disbursement found for ReferenceId '{payload.ReferenceId}'.");
        }

        // Idempotency — already processed
        if (disbursement.DisbursementStatus == DisbursementStatus.Completed ||
            disbursement.DisbursementStatus == DisbursementStatus.Failed)
        {
            _logger.LogWarning("[NAPAS] Duplicate callback for ReferenceId={ReferenceId}. Already {Status}.",
                payload.ReferenceId, disbursement.DisbursementStatus);
            return Result.Success(new CallbackHandleResult
            {
                DisbursementId = disbursement.Id,
                NewStatus      = disbursement.DisbursementStatus,
                IsSuccess      = disbursement.DisbursementStatus == DisbursementStatus.Completed
            });
        }

        var isSuccess = payload.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
        disbursement.DisbursementStatus  = isSuccess ? DisbursementStatus.Completed : DisbursementStatus.Failed;
        disbursement.NapasTransactionId  = payload.NapasTransactionId;
        disbursement.GatewayConfirmedAt  = payload.ProcessedAt;
        disbursement.GatewayErrorMessage = isSuccess ? null : $"{payload.ErrorCode}: {payload.ErrorMessage}";

        await _db.SaveChangesAsync();

        _logger.LogInformation("[NAPAS] Disbursement {Id} updated → {Status}.", disbursement.Id, disbursement.DisbursementStatus);

        return Result.Success(new CallbackHandleResult
        {
            DisbursementId = disbursement.Id,
            NewStatus      = disbursement.DisbursementStatus,
            IsSuccess      = isSuccess
        });
    }

    // ── [QUERY] QueryTransactionStatusAsync ─────────────────────────────────

    public async Task<Result<PaymentGatewayResponse>> QueryTransactionStatusAsync(string napasReferenceId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/transfers/{napasReferenceId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return Result.Failure<PaymentGatewayResponse>(
                    $"NAPAS query failed: HTTP {(int)response.StatusCode}. RefId: {napasReferenceId}");

            var json = JsonSerializer.Deserialize<JsonElement>(body, _jsonOptions);
            var napasStatus = json.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            var txId        = json.TryGetProperty("transactionId", out var t) ? t.GetString() ?? "" : "";

            return Result.Success(new PaymentGatewayResponse
            {
                TransactionId          = txId,
                ReferenceId            = napasReferenceId,
                ProcessedAt            = DateTime.UtcNow,
                OriginalBankCode       = string.Empty,
                OriginalAccountNumber  = string.Empty,
                IsConfirmedImmediately = napasStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NAPAS] QueryStatus error. ReferenceId={ReferenceId}", napasReferenceId);
            return Result.Failure<PaymentGatewayResponse>($"NAPAS query error: {ex.Message}");
        }
    }

    // ── [VERIFY] VerifyCallbackSignature ────────────────────────────────────
    // Dùng từ NapasCallbackController để verify header X-Napas-Signature

    /// <summary>
    /// Verify chữ ký HMAC-SHA256 từ NAPAS callback.
    /// NAPAS ký: HMAC-SHA256(rawBody, CallbackSecret)
    /// </summary>
    public bool VerifyCallbackSignature(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.CallbackSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.CallbackSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        var actual   = signatureHeader.ToLowerInvariant().TrimStart("sha256=".ToCharArray());

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string GenerateReferenceId() =>
        $"LF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
}
