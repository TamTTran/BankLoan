using LoanFlow.Application.Services;

namespace LoanFlow.API.BackgroundJobs;

/// <summary>
/// ASP.NET Core BackgroundService that runs the daily overdue processing job.
/// Checks every 60 seconds if it's past the configured run time and hasn't run today.
/// Designed for single-instance deployment (Docker Compose).
/// </summary>
public class DailyOverdueBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyOverdueBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    private DateOnly _lastRunDate = DateOnly.MinValue;

    public DailyOverdueBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DailyOverdueBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue("BackgroundJobs:DailyOverdue:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("DailyOverdueBackgroundService is DISABLED via configuration.");
            return;
        }

        var runAtHour = _configuration.GetValue("BackgroundJobs:DailyOverdue:RunAtHour", 1);
        var runAtMinute = _configuration.GetValue("BackgroundJobs:DailyOverdue:RunAtMinute", 0);
        var batchSize = _configuration.GetValue("BackgroundJobs:DailyOverdue:BatchSize", 100);

        _logger.LogInformation(
            "DailyOverdueBackgroundService started. Scheduled at {Hour:D2}:{Minute:D2} (UTC+7), batch size: {BatchSize}",
            runAtHour, runAtMinute, batchSize);

        // Check every 60 seconds
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var vnNow = GetVietnamTime();
                var calendar = _serviceProvider.GetRequiredService<IBusinessCalendarService>();
                var today = calendar.GetCurrentBusinessDate();

                // Skip if already ran today
                if (_lastRunDate == today) continue;

                // Skip if not yet at the scheduled time
                if (vnNow.Hour < runAtHour || (vnNow.Hour == runAtHour && vnNow.Minute < runAtMinute))
                    continue;

                _logger.LogInformation("Daily overdue job triggered for business date {Date}", today);

                await RunJobAsync(today, batchSize, stoppingToken);
                _lastRunDate = today;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("DailyOverdueBackgroundService is shutting down.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in DailyOverdueBackgroundService loop.");
                // Don't crash the host — wait for the next tick
            }
        }
    }

    private async Task RunJobAsync(DateOnly businessDate, int batchSize, CancellationToken ct)
    {
        // Create a scope because IOverdueJobService is scoped (depends on DbContext)
        using var scope = _serviceProvider.CreateScope();
        var jobService = scope.ServiceProvider.GetRequiredService<IEndOfDayService>();

        // System user ID = null inside background job
        var result = await jobService.RunAsync(businessDate, null, ct);

        if (result.Errors > 0)
        {
            _logger.LogWarning(
                "EOD job for {Date} completed WITH ERRORS: {Errors} errors out of {Total} active loans",
                businessDate, result.Errors, result.TotalActiveLoans);
        }
    }

    private DateTime GetVietnamTime()
    {
        var timeZoneId = _configuration["BusinessDate:TimeZoneId"] ?? "SE Asia Standard Time";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }
}
