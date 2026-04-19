namespace LoanFlow.Domain.Entities;

public class GLAccount
{
    public int Id { get; set; }
    public string AccountCode { get; set; } = default!;     // e.g., "1001" for Cash, "1301" for Loan Principal
    public string AccountName { get; set; } = default!;
    
    // Asset, Liability, Equity, Revenue, Expense
    public string AccountType { get; set; } = default!;
    
    public decimal Balance { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
