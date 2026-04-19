namespace LoanFlow.Domain.Entities;

/// <summary>
/// Represents a bank branch for multi-branch runtime support.
/// </summary>
public class Branch
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = default!;   // unique: "HCM-HQ", "HN-01"
    public string BranchName { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public int? ManagerUserId { get; set; }
    
    /// <summary>Active | Inactive</summary>
    public string Status { get; set; } = "Active";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? Manager { get; set; }
}
