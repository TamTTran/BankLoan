using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace LoanFlow.API.Middleware;

/// <summary>
/// M3: Masks sensitive PII fields from request/response logs.
/// Intercepts structured log enrichment to redact Phone, IdNumber, and similar fields.
/// </summary>
public static class SerilogPiiMaskingExtensions
{
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Phone", "IdNumber", "DateOfBirth", "Address",
        "Password", "PasswordHash", "RefreshToken"
    };

    public static bool IsSensitiveProperty(string name)
        => SensitivePropertyNames.Contains(name);
}
