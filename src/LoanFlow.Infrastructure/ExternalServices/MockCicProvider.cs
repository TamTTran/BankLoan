using LoanFlow.Application.Services;
using System.Text.Json;

namespace LoanFlow.Infrastructure.ExternalServices;

/// <summary>
/// Mock CIC provider for development/testing.
/// Generates realistic-looking CIC scores based on random seed + customer name hash.
/// Replace with real NAPAS/PCB API integration in production.
/// </summary>
public class MockCicProvider : ICicProvider
{
    public string ProviderName => "MOCK";

    public Task<CicInquiryResultDto> InquireAsync(string idNumber, string fullName)
    {
        // Deterministic seed based on ID number for consistent results
        var seed = idNumber.GetHashCode();
        var rng = new Random(Math.Abs(seed));

        var score = rng.Next(300, 850);  // CIC scores typically 300-850
        var grade = score switch
        {
            >= 750 => "AA",
            >= 680 => "A",
            >= 600 => "BB",
            >= 500 => "B",
            >= 400 => "C",
            _ => "D"
        };

        var totalBorrowing = Math.Round((decimal)(rng.NextDouble() * 500_000_000), 0);  // 0 - 500M VND
        var maxDpd = rng.Next(0, 90);

        var result = new CicInquiryResultDto
        {
            Score = score,
            Grade = grade,
            TotalBorrowing = totalBorrowing,
            MaxDpd = maxDpd,
            IsSuccess = true,
            RawJson = JsonSerializer.Serialize(new
            {
                provider = "MOCK",
                timestamp = DateTime.UtcNow,
                idNumber,
                fullName,
                score,
                grade,
                totalBorrowing,
                maxDpd,
                disclaimer = "This is mock data for development purposes only."
            })
        };

        // Simulate network latency
        return Task.FromResult(result);
    }
}
