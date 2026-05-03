using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;

namespace LoanFlow.Application.Services;

public interface IAccountingService
{
    Task<Result<JournalEntry>> PostJournalAsync(JournalEntryDto request);
    Task<string> GetAccountCodeAsync(string transactionType, bool isDebit);
}
