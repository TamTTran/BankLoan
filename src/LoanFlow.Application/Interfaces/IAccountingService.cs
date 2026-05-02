using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IAccountingService
{
    Task<Result<JournalEntry>> PostJournalAsync(JournalEntryDto request);
    Task<string> GetAccountCodeAsync(string transactionType, bool isDebit);
}
