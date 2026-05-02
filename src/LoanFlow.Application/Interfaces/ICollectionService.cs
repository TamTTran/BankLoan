using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ICollectionService
{
    Task<IEnumerable<CollectionQueueItemDto>> GetCollectorQueueAsync(int collectorUserId);
    Task<IEnumerable<CollectionQueueItemDto>> GetAllCollectionQueueAsync();
    Task<Result> AssignCollectorAsync(int loanId, int collectorUserId, int assignedByUserId);
    Task<Result> AddCollectionActionAsync(int loanId, CollectionActionDto dto, int userId);
    Task<IEnumerable<CollectionActionItemDto>> GetCollectionHistoryAsync(int loanId);
    Task<int> CheckBrokenPromisesAsync(DateOnly businessDate);
    Task<int> ApplyEscalationRulesAsync(DateOnly businessDate);
    Task<CollectionSummaryDto> GetCollectionSummaryAsync();
    Task<Result> AddCollectionNoteAsync(int loanId, LoanFlow.Application.DTOs.Loan.AddCollectionNoteRequest req, int userId);
}
