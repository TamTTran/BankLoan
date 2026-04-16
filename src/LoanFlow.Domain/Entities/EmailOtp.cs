namespace LoanFlow.Domain.Entities;

/// <summary>
/// Tracks email OTPs and deeplink tokens with a TTL (e.g. 10 minutes).
/// </summary>
public class EmailOtp
{
    public int Id { get; set; }
    public int UserId { get; set; }
    
    public string Email { get; set; } = default!;
    public string OtpCode { get; set; } = default!; // Typically 6 digits
    
    /// <summary>Login | ResetPassword | ApproveAction | VerifyOverpayment</summary>
    public string Purpose { get; set; } = default!;
    
    public string? DeeplinkToken { get; set; } // GUID for direct one-click bypass if applicable
    public string? DeeplinkUrl { get; set; }
    
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public User User { get; set; } = default!;
    
    public bool IsValid() => !IsUsed && DateTime.UtcNow <= ExpiresAt;
}
