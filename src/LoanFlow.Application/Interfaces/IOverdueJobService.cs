using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IOverdueJobService
{
    /// <summary>
    /// Run the daily overdue processing for the given business date.
    /// Idempotent: safe to call multiple times for the same date.
    /// </summary>
    Task<OverdueJobResultDto> RunDailyOverdueProcessingAsync(DateOnly businessDate, int batchSize = 100, CancellationToken ct = default);

    /// <summary>
    /// Query snapshot history for a date range.
    /// </summary>
    Task<List<SnapshotSummaryDto>> GetSnapshotHistoryAsync(DateOnly from, DateOnly to);
}
