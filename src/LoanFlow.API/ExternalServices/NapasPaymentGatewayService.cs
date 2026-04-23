using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// H10: Production NAPAS/ViettelPay Payment Gateway integration.
/// Configured via KeyVault: ExternalServices:NapasApiUrl, ExternalServices:NapasApiKey.
/// </summary>
public class NapasPaymentGatewayService : IPaymentGatewayService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NapasPaymentGatewayService> _logger;
    private readonly string _apiUrl;
    private readonly string _apiKey;

    public NapasPaymentGatewayService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<NapasPaymentGatewayService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiUrl = config["ExternalServices:NapasApiUrl"]
            ?? throw new InvalidOperationException("ExternalServices:NapasApiUrl is not configured.");
        _apiKey = config["ExternalServices:NapasApiKey"]
            ?? throw new InvalidOperationException("ExternalServices:NapasApiKey is not configured.");
    }

    public async Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(
        string bankCode, string accountNumber, decimal amount, string remark)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<PaymentGatewayResponse>("Bank account number is required.");
        if (string.IsNullOrWhiteSpace(bankCode))
            return Result.Failure<PaymentGatewayResponse>("Bank code is required.");
        if (amount <= 0)
            return Result.Failure<PaymentGatewayResponse>("Transfer amount must be greater than zero.");

        try
        {
            var payload = new
            {
                bankCode,
                accountNumber,
                amount,
                remark,
                referenceId = $"LF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"
            };

            // BUG-002: Use per-request headers instead of DefaultRequestHeaders (thread-safe)
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/transfers");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("NAPAS transfer failed: {Status}", response.StatusCode);
                return Result.Failure<PaymentGatewayResponse>($"Payment gateway error: HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            return Result.Success(new PaymentGatewayResponse
            {
                TransactionId = result.GetProperty("transactionId").GetString() ?? "",
                ProcessedAt = DateTime.UtcNow,
                OriginalBankCode = bankCode,
                OriginalAccountNumber = accountNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NAPAS payment gateway exception");
            return Result.Failure<PaymentGatewayResponse>($"Payment gateway unavailable: {ex.Message}");
        }
    }
}
