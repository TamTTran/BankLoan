using LoanFlow.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// H10: Production Twilio SMS integration.
/// Configured via KeyVault: Twilio:AccountSid, Twilio:AuthToken, Twilio:FromNumber.
/// </summary>
public class TwilioSmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;

    public TwilioSmsService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<TwilioSmsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _accountSid = config["Twilio:AccountSid"]
            ?? throw new InvalidOperationException("Twilio:AccountSid is not configured.");
        _authToken = config["Twilio:AuthToken"]
            ?? throw new InvalidOperationException("Twilio:AuthToken is not configured.");
        _fromNumber = config["Twilio:FromNumber"]
            ?? throw new InvalidOperationException("Twilio:FromNumber is not configured.");
    }

    public async Task SendAsync(string phone, string message)
    {
        try
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{_authToken}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var payload = new Dictionary<string, string>
            {
                ["To"] = phone,
                ["From"] = _fromNumber,
                ["Body"] = message
            };

            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json";
            var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(payload));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Twilio SMS failed to {Phone}: HTTP {Status}", phone, response.StatusCode);
                return;
            }

            _logger.LogInformation("SMS sent successfully to {Phone}", phone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio SMS exception sending to {Phone}", phone);
        }
    }
}
