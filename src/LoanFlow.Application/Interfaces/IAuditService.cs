using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IAuditService
{
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(string? entityType, int? entityId, int? userId, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 50);
}
