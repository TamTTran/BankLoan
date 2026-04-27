namespace LoanFlow.Domain.Entities;

public class InterestRateHistory
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    
    /// <summary>Lãi suất cơ sở (ví dụ: Lãi suất tiết kiệm 12 tháng)</summary>
    public decimal BaseRate { get; set; }
    
    /// <summary>Biên độ cộng thêm</summary>
    public decimal Margin { get; set; }
    
    /// <summary>Lãi suất thực tế áp dụng = BaseRate + Margin</summary>
    public decimal EffectiveRate => BaseRate + Margin;
    
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }

    public Loan Loan { get; set; } = default!;
}
