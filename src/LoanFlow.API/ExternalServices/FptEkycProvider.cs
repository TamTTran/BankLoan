using LoanFlow.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// H10: Production FPT.AI / VNPT eKYC integration.
/// Configured via KeyVault: ExternalServices:FptEkycApiUrl, ExternalServices:FptEkycApiKey.
/// </summary>
public class FptEkycProvider : IEkycProvider
{
    public string ProviderName => "FPT";

    private readonly HttpClient _httpClient;
    private readonly ILogger<FptEkycProvider> _logger;
    private readonly string _apiUrl;
    private readonly string _apiKey;

    public FptEkycProvider(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<FptEkycProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiUrl = config["ExternalServices:FptEkycApiUrl"]
            ?? throw new InvalidOperationException("ExternalServices:FptEkycApiUrl is not configured.");
        _apiKey = config["ExternalServices:FptEkycApiKey"]
            ?? throw new InvalidOperationException("ExternalServices:FptEkycApiKey is not configured.");
    }

    public async Task<EkycResultDto> VerifyAsync(string idNumber, string idType, string fullName)
    {
        try
        {
            var payload = new { idNumber, idType, fullName };

            // BUG-002: Use per-request headers instead of DefaultRequestHeaders (thread-safe)
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/verify");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FPT eKYC verification failed: {Status}", response.StatusCode);
                return new EkycResultDto { IsSuccess = false, RawJson = $"HTTP {(int)response.StatusCode}" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            return new EkycResultDto
            {
                FullNameExtracted = result.GetProperty("fullNameExtracted").GetString() ?? "",
                FaceMatchScore = result.GetProperty("faceMatchScore").GetDecimal(),
                OcrConfidence = result.GetProperty("ocrConfidence").GetDecimal(),
                LivenessScore = result.GetProperty("livenessScore").GetDecimal(),
                IsSuccess = true,
                RawJson = json
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FPT eKYC exception for idNumber: {IdNumber}", idNumber);
            return new EkycResultDto { IsSuccess = false, RawJson = ex.Message };
        }
    }
}
