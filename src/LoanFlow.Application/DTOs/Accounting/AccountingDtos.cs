using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class JournalEntryDto
{
    public string ReferenceNumber { get; set; } = default!;
    public DateOnly BusinessDate { get; set; }
    public string Description { get; set; } = default!;
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryLineDto
{
    public string AccountCode { get; set; } = default!;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? LineDescription { get; set; }
}
