using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;

namespace LoanFlow.Application.Services;

public interface IAuditService
{
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(string? entityType, int? entityId, int? userId, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 50);
}
