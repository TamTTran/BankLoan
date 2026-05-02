using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace LoanFlow.Application.Services;

public class EmailService : IEmailService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(LoanFlowDbContext db, ILogger<EmailService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<string>> SendOtpAsync(int userId, string email, string purpose, string? deeplinkUrl = null)
    {
        // Rate limit: max 5 OTPs per user per hour
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentCount = await _db.EmailOtps.CountAsync(o => o.UserId == userId && o.CreatedAt >= oneHourAgo);
        if (recentCount >= 5)
            return Result.Failure<string>("Too many OTP requests. Please try again later.");

        // Generate cryptographically secure 6-digit OTP
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var deeplinkToken = Guid.NewGuid().ToString("N");

        var otp = new EmailOtp
        {
            UserId = userId,
            Email = email,
            OtpCode = otpCode,
            Purpose = purpose,
            DeeplinkToken = deeplinkToken,
            DeeplinkUrl = deeplinkUrl,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _db.EmailOtps.Add(otp);
        await _db.SaveChangesAsync();

        _logger.LogInformation("🚀 [EMAIL STUB] Sent OTP {OtpCode} to {Email} for {Purpose}. TTL: 10m. URL: {Url}?token={Token}", 
            otpCode, email, purpose, deeplinkUrl, deeplinkToken);

        return Result.Success(deeplinkToken);
    }

    public async Task<Result> VerifyOtpAsync(int userId, string otpCode, string? deeplinkToken = null)
    {
        var query = _db.EmailOtps.Where(o => o.UserId == userId); // Always scope to user

        if (!string.IsNullOrEmpty(deeplinkToken))
            query = query.Where(o => o.DeeplinkToken == deeplinkToken);
        else if (!string.IsNullOrEmpty(otpCode))
            query = query.Where(o => o.OtpCode == otpCode);
        else
            return Result.Failure("Provide either an OTP code or a deeplink token.");

        var otp = await query.OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();

        if (otp == null) return Result.Failure("OTP not found.");
        if (otp.IsUsed) return Result.Failure("OTP has already been used.");
        if (!otp.IsValid()) return Result.Failure("OTP has expired.");

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        return Result.Success();
    }
}
