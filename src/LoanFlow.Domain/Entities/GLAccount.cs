namespace LoanFlow.Domain.Entities;

public class GLAccount
{
    public int Id { get; set; }
    public string AccountCode { get; set; } = default!;     // e.g., "1001" for Cash, "1301" for Loan Principal
    public string AccountName { get; set; } = default!;
    
    // Asset, Liability, Equity, Revenue, Expense
    public string AccountType { get; set; } = default!;
    
    public decimal Balance { get; set; }

    // ── Nâng cấp Core Banking ────────────────────────────────────────────────
    public int? ParentAccountId { get; set; }
    public int? BranchId { get; set; }
    public string CurrencyCode { get; set; } = "VND";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public byte[] RowVersion { get; set; } = [];
}
