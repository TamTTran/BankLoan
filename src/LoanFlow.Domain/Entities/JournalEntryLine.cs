namespace LoanFlow.Domain.Entities;

public class JournalEntryLine
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }
    public int GLAccountId { get; set; }
    
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    
    public string? LineDescription { get; set; }
    
    public JournalEntry JournalEntry { get; set; } = default!;
    public GLAccount GLAccount { get; set; } = default!;
}
