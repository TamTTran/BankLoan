using System;

namespace LoanFlow.Domain.Entities;

public class GLAccountMapping
{
    public int Id { get; set; }
    public string TransactionType { get; set; } = default!; // "Disbursement", "Payment", "LateFee", etc.
    public string DebitAccountCode { get; set; } = default!;
    public string CreditAccountCode { get; set; } = default!;
    public string Status { get; set; } = "Active";
    public string? Description { get; set; }
}
