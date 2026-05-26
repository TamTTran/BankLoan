namespace LoanFlow.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Role { get; set; } = default!; // Admin | LoanOfficer | Approver | Collector | Manager
    public string Status { get; set; } = "Active"; // Active | Inactive
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public int? BranchId { get; set; }              // FK to Branch (null = HQ / all branches)
    
    // ── Module 6: Security & Compliance ───────────────────────────────────────
    public DateTime? PasswordExpiresAt { get; set; }
    public DateTime? LastPasswordChangeAt { get; set; }
    public bool MfaEnabled { get; set; } = false;
    public string? MfaSecret { get; set; }

    // ── SEC-005: Last login tracking ───────────────────────────────────────────
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();
}
