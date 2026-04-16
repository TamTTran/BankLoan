using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public interface ISmsService
{
    Task SendAsync(string phone, string message);
}

public class NullSmsService : ISmsService
{
    private readonly ILogger<NullSmsService> _logger;

    public NullSmsService(ILogger<NullSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phone, string message)
    {
        // Stub: Ready for Twilio/VNPT swap matching user's architecture request.
        _logger.LogInformation("📱 [SMS STUB] Sending to {Phone}: {Message}", phone, message);
        return Task.CompletedTask;
    }
}
