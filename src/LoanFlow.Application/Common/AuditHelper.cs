using System.Text.Json;

namespace LoanFlow.Application.Common;

/// <summary>
/// Safe JSON serializer for AuditLog fields.
/// Prevents JSON/log injection from string interpolation.
/// </summary>
public static class AuditHelper
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string ToJson(object value)
        => JsonSerializer.Serialize(value, _opts);
}
