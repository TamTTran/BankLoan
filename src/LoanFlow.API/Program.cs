using LoanFlow.Application.Services;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// === Database ===
builder.Services.AddDbContext<LoanFlowDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// === Services ===
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILoanProductService, LoanProductService>();
builder.Services.AddScoped<ILoanApplicationService, LoanApplicationService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// === JWT Auth ===
var jwtKey = builder.Configuration["Jwt:Key"]!;
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

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

// === CORS (for frontend) ===
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", p =>
        p.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:5500")
         .AllowAnyHeader()
         .AllowAnyMethod()));

var app = builder.Build();

// === Middleware ===
app.UseCors("AllowFrontend");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LoanFlow Lite API v2");
    c.DocumentTitle = "LoanFlow Lite API v2";
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// === Migrate + Seed (with retry for Docker) ===
// SQL Server trong Docker có thể chưa sẵn sàng khi API start
await MigrateWithRetryAsync(app);

app.Run();

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
            await DbSeeder.SeedAsync(db);
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
    await DbSeeder.SeedAsync(db);
}
