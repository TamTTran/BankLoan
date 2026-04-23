using LoanFlow.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// H10: Production PCB (Vietnam Credit Bureau) CIC integration.
/// Configured via KeyVault: ExternalServices:PcbApiUrl, ExternalServices:PcbApiKey.
/// </summary>
public class PcbCicProvider : ICicProvider
{
    public string ProviderName => "PCB";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PcbCicProvider> _logger;
    private readonly string _apiUrl;
    private readonly string _apiKey;

    public PcbCicProvider(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<PcbCicProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiUrl = config["ExternalServices:PcbApiUrl"]
            ?? throw new InvalidOperationException("ExternalServices:PcbApiUrl is not configured.");
        _apiKey = config["ExternalServices:PcbApiKey"]
            ?? throw new InvalidOperationException("ExternalServices:PcbApiKey is not configured.");
    }

    public async Task<CicInquiryResultDto> InquireAsync(string idNumber, string fullName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var payload = new { idNumber, fullName };

            var response = await _httpClient.PostAsync(
                $"{_apiUrl}/inquiries",
                new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PCB CIC inquiry failed: {Status}", response.StatusCode);
                return new CicInquiryResultDto { IsSuccess = false, RawJson = $"HTTP {(int)response.StatusCode}" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            return new CicInquiryResultDto
            {
                Score = result.GetProperty("creditScore").GetInt32(),
                Grade = result.GetProperty("creditGrade").GetString() ?? "",
                TotalBorrowing = result.GetProperty("totalBorrowing").GetDecimal(),
                MaxDpd = result.GetProperty("maxDpd").GetInt32(),
                IsSuccess = true,
                RawJson = json
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PCB CIC inquiry exception for idNumber: {IdNumber}", idNumber);
            return new CicInquiryResultDto { IsSuccess = false, RawJson = ex.Message };
        }
    }
}
