using Microsoft.AspNetCore.Http;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoanFlow.API.Middleware;

public class DataMaskingMiddleware
{
    private readonly RequestDelegate _next;

    // Masking rules for common sensitive fields in Vietnamese context
    private static readonly Regex PhoneRegex = new Regex(@"(?<!\d)(0[3|5|7|8|9]\d{8})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex IdNumberRegex = new Regex(@"(?<!\d)(\d{9}|\d{12})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new Regex(@"([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})", RegexOptions.Compiled);

    public DataMaskingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Admin sees full data
        if (context.User.IsInRole("Admin"))
        {
            await _next(context);
            return;
        }

        // We only mask JSON responses
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        if (context.Response.ContentType != null && context.Response.ContentType.Contains("application/json"))
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(json))
            {
                var maskedJson = ApplyMasking(json);
                context.Response.Body = originalBodyStream;
                var bytes = Encoding.UTF8.GetBytes(maskedJson);
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                return;
            }
        }

        // If not JSON or empty, just copy it back
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);
        context.Response.Body = originalBodyStream;
    }

    private string ApplyMasking(string json)
    {
        var result = json;

        // Mask Phone Numbers (098****321)
        result = PhoneRegex.Replace(result, match =>
        {
            var phone = match.Groups[1].Value;
            if (phone.Length == 10)
                return phone.Substring(0, 3) + "****" + phone.Substring(7, 3);
            return phone;
        });

        // Mask ID Numbers / CCCD (0012****89)
        result = IdNumberRegex.Replace(result, match =>
        {
            var id = match.Groups[1].Value;
            if (id.Length == 9)
                return id.Substring(0, 3) + "***" + id.Substring(6, 3);
            if (id.Length == 12)
                return id.Substring(0, 4) + "****" + id.Substring(8, 4);
            return id;
        });

        // Mask Emails (t***@domain.com)
        result = EmailRegex.Replace(result, match =>
        {
            var user = match.Groups[1].Value;
            var domain = match.Groups[2].Value;
            if (user.Length > 2)
                return user.Substring(0, 1) + "***" + user.Substring(user.Length - 1, 1) + "@" + domain;
            return user + "@" + domain;
        });

        return result;
    }
}
