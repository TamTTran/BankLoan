namespace LoanFlow.API.Middleware;

/// <summary>
/// SEC-004: Adds HTTP security headers to all API responses.
/// Protects against MIME-sniffing, clickjacking, caching of sensitive data,
/// and transport downgrade attacks.
/// These complement the headers already set by Nginx for the frontend.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env  = env;
    }

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

        // H-2: Content-Security-Policy — API returns JSON only; no scripts/styles needed.
        // Swagger UI is exempt in Development (it needs inline scripts).
        if (!_env.IsDevelopment())
        {
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none';";
        }

        // M-1: HSTS — tell browsers to only connect via HTTPS for 1 year.
        // Only set on HTTPS requests so HTTP→HTTPS redirects aren't affected.
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        // Remove headers that leak technology stack info.
        // OnStarting fires just before response headers are flushed — guaranteed last.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
