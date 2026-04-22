using LoanFlow.API.Middleware;
using LoanFlow.Application.Services;
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

// === Database ===
builder.Services.AddDbContext<LoanFlowDbContext>(options =>
    options.UseMySql(connString, ServerVersion.AutoDetect(connString)));

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
builder.Services.AddScoped<IPaymentGatewayService, MockPaymentGatewayService>(); // Phase 4
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, NullSmsService>();
builder.Services.AddScoped<IEndOfDayService, EndOfDayService>();
builder.Services.AddScoped<IBodService, BodService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IApprovalFlowService, ApprovalFlowService>();
builder.Services.AddScoped<ILoanAdjustmentService, LoanAdjustmentService>();
builder.Services.AddScoped<ILoanLifecycleService, LoanLifecycleService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();

// === Phase 4: Enterprise Banking ===
builder.Services.AddScoped<INplReportingService, NplReportingService>();
builder.Services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
builder.Services.AddScoped<IRegulatoryReportingService, RegulatoryReportingService>();
builder.Services.AddScoped<ICicProvider, LoanFlow.API.ExternalServices.MockCicProvider>();
builder.Services.AddScoped<IEkycProvider, LoanFlow.API.ExternalServices.MockEkycProvider>();
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
builder.Services.AddScoped<ICertificateAuthorityProvider, MockCertificateAuthorityProvider>();
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

// === Health Checks (used by Docker healthcheck) ===
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LoanFlowDbContext>("database");

// === Rate Limiting ===
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("RefreshRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
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
         .AllowAnyMethod()));



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
app.MapControllers();

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
