using LoanFlow.Application.Services;
using System.Text.Json;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Mock eKYC provider for development/testing.
/// Generates realistic verification results.
/// Replace with real VNPT/FPT eKYC API integration in production.
/// </summary>
public class MockEkycProvider : IEkycProvider
{
    public string ProviderName => "MOCK";

    public Task<EkycResultDto> VerifyAsync(string idNumber, string idType, string fullName)
    {
        var seed = idNumber.GetHashCode();
        var rng = new Random(Math.Abs(seed));

        var faceMatchScore = Math.Round((decimal)(85 + rng.NextDouble() * 15), 1);
        var ocrConfidence = Math.Round((decimal)(88 + rng.NextDouble() * 12), 1);

        var result = new EkycResultDto
        {
            FullNameExtracted = fullName,
            FaceMatchScore = faceMatchScore,
            OcrConfidence = ocrConfidence,
            IsSuccess = true,
            RawJson = JsonSerializer.Serialize(new
            {
                provider = "MOCK",
                timestamp = DateTime.UtcNow,
                idNumber, idType, fullNameExtracted = fullName, faceMatchScore, ocrConfidence,
                disclaimer = "This is mock data for development purposes only."
            })
        };

        return Task.FromResult(result);
    }
}
