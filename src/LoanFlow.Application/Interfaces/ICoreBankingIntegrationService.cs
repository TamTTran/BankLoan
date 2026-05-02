using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ICoreBankingIntegrationService
{
    Task<Result> PushJournalEntryAsync(int journalEntryId);
    Task<Result> PushDailyBatchAsync(DateOnly businessDate);
}
