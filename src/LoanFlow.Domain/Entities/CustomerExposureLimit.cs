namespace LoanFlow.Domain.Entities;

/// <summary>
/// Customer-level exposure limit configuration.
/// Controls maximum total borrowing per customer across all active loans.
/// </summary>
public class CustomerExposureLimit
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal MaxExposure { get; set; }            // VND — tổng dư nợ tối đa
    public decimal CurrentExposure { get; set; }        // VND — tổng dư nợ hiện tại (auto-calculated)
    public decimal UtilizationRate { get; set; }        // % = Current / Max * 100

    /// <summary>Normal | Warning | Exceeded | Overridden</summary>
    public string Status { get; set; } = "Normal";
    
    public int? OverriddenByUserId { get; set; }
    public string? OverrideReason { get; set; }
    public DateTime? OverriddenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = default!;
    public User? OverriddenBy { get; set; }
}
