using LoanFlow.API.Middleware;
using LoanFlow.API.ExternalServices;
using LoanFlow.Application.Services;
using LoanFlow.Application.Common;
using LoanFlow.API.BackgroundJobs;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Formatting.Compact;
using Azure.Identity;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Net;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // M11: Azure Key Vault Integration
    var keyVaultUri = builder.Configuration["KeyVaultUri"];
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUri),
            new DefaultAzureCredential());
    }

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Destructure.ByTransforming<object>(obj => obj) // base
        .Filter.ByExcluding(e =>
        {
            // M3: Scrub PII fields from structured log properties
            var props = e.Properties;
            return props.ContainsKey("Password") ||
                   props.ContainsKey("PasswordHash");
        })
        .WriteTo.Console(new CompactJsonFormatter()));

// === Startup Validation ===
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connString))
{
    throw new InvalidOperationException("FATAL: 'DefaultConnection' is missing from configuration.");
}
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("FATAL: 'Jwt:Key' is missing or too short (requires at least 32 characters for HMACSHA256).");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException("FATAL: 'Jwt:Issuer' is missing from configuration.");
}
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("FATAL: 'Jwt:Audience' is missing from configuration.");
}

ValidateProductionConfiguration(builder);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LoanFlow.Infrastructure.Data.AuditLogInterceptor>();

// === Database ===
builder.Services.AddDbContext<LoanFlowDbContext>((sp, options) =>
{
    options.UseMySql(connString, ServerVersion.AutoDetect(connString));
    options.AddInterceptors(sp.GetRequiredService<LoanFlow.Infrastructure.Data.AuditLogInterceptor>());
});

builder.Services.Configure<BusinessDateOptions>(builder.Configuration.GetSection("BusinessDate"));
builder.Services.AddSingleton<IBusinessCalendarService, BusinessCalendarService>();
builder.Services.AddSingleton<IBusinessClock, BusinessClock>();

// H2: OpenTelemetry Configuration
var otel = builder.Services.AddOpenTelemetry();
otel.ConfigureResource(resource => resource.AddService("LoanFlow.API"));
otel.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddRuntimeInstrumentation()
    .AddProcessInstrumentation()
    .AddPrometheusExporter());
otel.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddEntityFrameworkCoreInstrumentation());

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_048_576; // 1MB limit to prevent oversized payload DoS
});

// === Services ===
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILoanProductService, LoanProductService>();
builder.Services.AddScoped<ILoanApplicationService, LoanApplicationService>();
builder.Services.AddScoped<ILateFeeService, LateFeeService>();
builder.Services.AddScoped<ICollateralService, CollateralService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<ICreditCheckService, CreditCheckService>();
builder.Services.AddScoped<IApprovalRuleService, ApprovalRuleService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICustomerLeadService, CustomerLeadService>();
builder.Services.AddScoped<IOverdueJobService, OverdueJobService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IReversalService, ReversalService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
// H10: Feature-flagged external service registrations (flip flags in appsettings / KeyVault for prod)
var extSvc = builder.Configuration.GetSection("ExternalServices");

// ── NAPAS Options (strongly-typed) ───────────────────────────────────────────────
builder.Services.Configure<NapasOptions>(builder.Configuration.GetSection(NapasOptions.SectionName));

// ── Payment Gateway: Mock (dev) │ Real NAPAS + Polly (prod) ─────────────────
if (extSvc.GetValue<bool>("UseMockPaymentGateway", defaultValue: true))
{
    // MockNapasPaymentGatewayService: giả lập đầy đủ sync + async callback
    // Test scenarios: BankCode="FAIL" (lỗi ngay), BankCode="ASYNC" (chờ callback)
    builder.Services.AddScoped<IPaymentGatewayService, MockNapasPaymentGatewayService>();
}
else
{
    var napasOpts = builder.Configuration.GetSection(NapasOptions.SectionName).Get<NapasOptions>()
        ?? new NapasOptions();

    builder.Services
        .AddHttpClient<IPaymentGatewayService, NapasPaymentGatewayService>(client =>
        {
            if (!string.IsNullOrWhiteSpace(napasOpts.ApiUrl))
                client.BaseAddress = new Uri(napasOpts.ApiUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(napasOpts.TimeoutSeconds * (napasOpts.MaxRetryAttempts + 1));
        })
        .AddStandardResilienceHandler(options =>
        {
            // Polly: Retry + timeout pipeline
            options.Retry.MaxRetryAttempts  = napasOpts.MaxRetryAttempts;
            options.Retry.Delay             = TimeSpan.FromMilliseconds(napasOpts.RetryBaseDelayMs);
            options.Retry.BackoffType        = Polly.DelayBackoffType.Exponential;
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(napasOpts.TimeoutSeconds * (napasOpts.MaxRetryAttempts + 1));
        });
}

if (extSvc.GetValue<bool>("UseMockSms", defaultValue: true))
    builder.Services.AddScoped<ISmsService, NullSmsService>();
else
    builder.Services.AddHttpClient<ISmsService, TwilioSmsService>();

builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEndOfDayService, EndOfDayService>();
builder.Services.AddScoped<IBodService, BodService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IApprovalFlowService, ApprovalFlowService>();
builder.Services.AddScoped<ILoanAdjustmentService, LoanAdjustmentService>();
builder.Services.AddScoped<ILoanLifecycleService, LoanLifecycleService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<ITotpService, TotpService>();

// === Phase 4: Enterprise Banking ===
builder.Services.AddScoped<INplReportingService, NplReportingService>();
builder.Services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
builder.Services.AddScoped<IRegulatoryReportingService, RegulatoryReportingService>();
builder.Services.AddScoped<ICapitalAdequacyService, CapitalAdequacyService>();
builder.Services.AddScoped<ILiquidityReportService, LiquidityReportService>();
builder.Services.AddScoped<IInterestRateReportService, InterestRateReportService>();
builder.Services.AddScoped<IRiskModelService, RiskModelService>();
builder.Services.AddScoped<IStressTestService, StressTestService>();
builder.Services.AddScoped<IConcentrationLimitService, ConcentrationLimitService>();
builder.Services.AddScoped<ISanctionListProvider, MockSanctionListProvider>();
builder.Services.AddScoped<IAmlScreeningService, AmlScreeningService>();
builder.Services.AddScoped<ITransactionMonitoringService, TransactionMonitoringService>();
builder.Services.AddScoped<IFinancialStatementService, FinancialStatementService>();
builder.Services.AddScoped<IVirtualAccountMatchingService, VirtualAccountMatchingService>();
builder.Services.AddScoped<ICoreBankingIntegrationService, CoreBankingIntegrationService>();
if (extSvc.GetValue<bool>("UseMockCic", defaultValue: true))
    builder.Services.AddScoped<ICicProvider, LoanFlow.API.ExternalServices.MockCicProvider>();
else
    builder.Services.AddHttpClient<ICicProvider, LoanFlow.API.ExternalServices.PcbCicProvider>();

if (extSvc.GetValue<bool>("UseMockEkyc", defaultValue: true))
    builder.Services.AddScoped<IEkycProvider, LoanFlow.API.ExternalServices.MockEkycProvider>();
else
    builder.Services.AddHttpClient<IEkycProvider, LoanFlow.API.ExternalServices.FptEkycProvider>();
builder.Services.AddScoped<ICicService, CicService>();
builder.Services.AddScoped<IEkycService, EkycService>();
builder.Services.AddScoped<IBranchService, BranchService>();

// === Phase 5 & 6: Operations & Advanced Lending ===
builder.Services.AddScoped<IExposureLimitService, ExposureLimitService>();
builder.Services.AddScoped<ILoanTopUpService, LoanTopUpService>();
builder.Services.AddScoped<ICollateralLifecycleService, CollateralLifecycleService>();
builder.Services.AddScoped<IDunningService, DunningService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IEContractService, EContractService>();
if (extSvc.GetValue<bool>("UseMockCertificateAuthority", defaultValue: true))
    builder.Services.AddScoped<ICertificateAuthorityProvider, MockCertificateAuthorityProvider>();
else
    builder.Services.AddHttpClient<ICertificateAuthorityProvider, ExternalCertificateAuthorityProvider>();
builder.Services.AddScoped<IDataExportService, DataExportService>();

// === EOD Application Services ===
builder.Services.AddScoped<IAccrualJobService, AccrualJobService>();
builder.Services.AddScoped<IClassificationJobService, ClassificationJobService>();

// === Background Jobs ===
builder.Services.AddHostedService<DailyOverdueBackgroundService>();

// === JWT Auth ===
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1) // Tighten clock skew from default 5min
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// === Health Checks (used by Docker healthcheck) ===
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LoanFlowDbContext>("database");

// === Rate Limiting ===
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    var knownNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var address))
            options.KnownProxies.Add(address);
    }

    foreach (var network in knownNetworks)
    {
        var parts = network.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var prefixLength))
        {
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var globalPermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Global:PermitLimit", 100);
    var globalWindowSeconds = builder.Configuration.GetValue<int>("RateLimiting:Global:WindowSeconds", 1);
    var pingPermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Ping:PermitLimit", 100);
    var pingWindowSeconds = builder.Configuration.GetValue<int>("RateLimiting:Ping:WindowSeconds", 1);

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { success = false, error = new { code = "RATE_LIMITED", message = "Too many requests." } },
            cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;
        if (path.StartsWithSegments("/health")
            || path.StartsWithSegments("/metrics")
            || path.StartsWithSegments("/api/live"))
        {
            return RateLimitPartition.GetNoLimiter("probe");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPermitLimit,
                Window = TimeSpan.FromSeconds(globalWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("PingRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = pingPermitLimit,
                Window = TimeSpan.FromSeconds(pingWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("LoginRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("RefreshRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// === Swagger ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LoanFlow Lite API",
        Version = "v2",
        Description = "Loan Management System — LoanFlow Lite v2"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// === CORS (origins from config) ===
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("FATAL: 'Cors:AllowedOrigins' is missing or empty in production configuration.");
}
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));   // BUG-001: Required for HttpOnly refreshToken cookie



var app = builder.Build();

app.UseForwardedHeaders();

// === Middleware ===
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

// Swagger — Development only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LoanFlow Lite API v2");
        c.DocumentTitle = "LoanFlow Lite API v2";
        c.RoutePrefix = "swagger";
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<BranchFilterMiddleware>();
app.UseAuthorization();
app.UseMiddleware<LoanFlow.API.Middleware.DataMaskingMiddleware>();
app.MapControllers();
app.MapHub<LoanFlow.API.Hubs.NotificationHub>("/api/hubs/notification");
app.MapGet("/api/live", () => Results.Ok(new { success = true, status = "alive" }))
    .DisableRateLimiting();
app.MapGet("/api/ping", () => Results.Ok(new { success = true, data = "pong" }))
    .RequireRateLimiting("PingRateLimit");

// Health endpoint for Docker / load-balancer probes (no auth required)
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

var migrateOnStartup = ShouldRunMigrationsOnStartup(app);
var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);

if (migrateOnStartup || migrateOnly)
{
    // Schema rollout is opt-in outside Development so application startup
    // does not implicitly become a production migration job.
    await MigrateWithRetryAsync(app);
}
else
{
    app.Logger.LogInformation(
        "Skipping automatic database migration. Set DB_MIGRATE_ON_STARTUP=true or run with --migrate-only to apply schema changes.");
}

if (migrateOnly)
{
    app.Logger.LogInformation("Schema rollout completed via --migrate-only.");
    return;
}

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
static bool ShouldRunMigrationsOnStartup(WebApplication app)
{
    var configuredValue = app.Configuration["DB_MIGRATE_ON_STARTUP"];
    if (!string.IsNullOrWhiteSpace(configuredValue)
        && bool.TryParse(configuredValue, out var migrateOnStartup))
    {
        return migrateOnStartup;
    }

    return app.Environment.IsDevelopment();
}

static void ValidateProductionConfiguration(WebApplicationBuilder builder)
{
    if (builder.Environment.IsDevelopment())
        return;

    var config = builder.Configuration;
    var externalServices = config.GetSection("ExternalServices");

    var mockFlags = new[]
    {
        "UseMockPaymentGateway",
        "UseMockCic",
        "UseMockEkyc",
        "UseMockSms",
        "UseMockCertificateAuthority"
    };

    foreach (var flag in mockFlags)
    {
        if (externalServices.GetValue<bool>(flag, defaultValue: true))
            throw new InvalidOperationException($"FATAL: ExternalServices:{flag} must be false in Production.");
    }

    RequireConfiguredSecret(config, "ExternalServices:NapasApiUrl");
    RequireConfiguredSecret(config, "ExternalServices:NapasApiKey");
    RequireConfiguredSecret(config, "ExternalServices:PcbApiUrl");
    RequireConfiguredSecret(config, "ExternalServices:PcbApiKey");
    RequireConfiguredSecret(config, "ExternalServices:FptEkycApiUrl");
    RequireConfiguredSecret(config, "ExternalServices:FptEkycApiKey");
    RequireConfiguredSecret(config, "ExternalServices:CertificateAuthorityApiUrl");
    RequireConfiguredSecret(config, "ExternalServices:CertificateAuthorityApiKey");
    RequireConfiguredSecret(config, "Twilio:AccountSid");
    RequireConfiguredSecret(config, "Twilio:AuthToken");
    RequireConfiguredSecret(config, "Twilio:FromNumber");

    var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length == 0)
        throw new InvalidOperationException("FATAL: 'Cors:AllowedOrigins' is missing or empty in Production.");

    if (allowedOrigins.Any(IsLocalhostOrigin)
        && !config.GetValue<bool>("Cors:AllowLocalhostInProduction", defaultValue: false))
    {
        throw new InvalidOperationException("FATAL: localhost CORS origins are not allowed in Production.");
    }

    var allowedHosts = config["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("FATAL: 'AllowedHosts' must be set to the production host name(s).");

    var webhookSecret = config["Webhook:Secret"];
    if (string.IsNullOrWhiteSpace(webhookSecret) || webhookSecret == "SET_VIA_ENV_OR_USER_SECRETS")
        throw new InvalidOperationException("FATAL: 'Webhook:Secret' must be configured in Production.");

    var webhookActor = config["Webhook:SystemActorUsername"];
    if (string.IsNullOrWhiteSpace(webhookActor))
        throw new InvalidOperationException("FATAL: 'Webhook:SystemActorUsername' must be configured in Production.");
}

static void RequireConfiguredSecret(IConfiguration config, string key)
{
    var value = config[key];
    if (string.IsNullOrWhiteSpace(value)
        || value.StartsWith("SET_VIA_", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"FATAL: '{key}' must be configured in Production.");
    }
}

static bool IsLocalhostOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return false;

    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}

static string GetClientPartitionKey(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static async Task MigrateWithRetryAsync(WebApplication app)
{
    const int maxRetries = 5;
    const int delaySeconds = 5;

    using var scope = app.Services.CreateScope();
    var db    = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation("Migration attempt {Attempt}/{Max}...", attempt, maxRetries);
            await db.Database.MigrateAsync();

            // Seed only in Development
            if (app.Environment.IsDevelopment())
            {
                await DbSeeder.SeedAsync(db);
            }

            logger.LogInformation("Database ready.");
            return;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(
                "DB not ready (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s...",
                attempt, maxRetries, ex.Message, delaySeconds);

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
    }

    // Final attempt — let exception propagate and crash the app
    logger.LogInformation("Final migration attempt {Max}/{Max}...", maxRetries, maxRetries);
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        await DbSeeder.SeedAsync(db);
    }
}
