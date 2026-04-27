namespace LoanFlow.Domain.Entities;

public class ExchangeRate
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    
    /// <summary>Ví dụ: USD</summary>
    public string CurrencyCode { get; set; } = default!;
    
    /// <summary>Tỷ giá Mua vào</summary>
    public decimal BuyRate { get; set; }
    
    /// <summary>Tỷ giá Bán ra</summary>
    public decimal SellRate { get; set; }
    
    /// <summary>Tỷ giá hạch toán trung tâm</summary>
    public decimal MidRate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
