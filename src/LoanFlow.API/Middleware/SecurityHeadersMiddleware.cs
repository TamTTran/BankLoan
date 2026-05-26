namespace LoanFlow.API.Middleware;

/// <summary>
/// SEC-004: Adds HTTP security headers to all API responses.
/// Protects against MIME-sniffing, clickjacking, and caching of sensitive data.
/// These complement the headers already set by Nginx for the frontend.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Disallow embedding in iframes (prevents clickjacking)
        headers["X-Frame-Options"] = "DENY";

        // Do not send referrer to any third-party
        headers["Referrer-Policy"] = "no-referrer";

        // Disable camera/microphone/geolocation permissions
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // Prevent any caching of API responses (sensitive financial data)
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
        headers["Pragma"]        = "no-cache";
        headers["Expires"]       = "0";

        // Remove any Server header that leaks technology stack info
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
