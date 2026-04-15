using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface IAuditService
{
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(string? entityType, int? entityId, int? userId, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 50);
}

public class AuditService : IAuditService
{
    private readonly LoanFlowDbContext _db;

    public AuditService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditLog>> GetAuditLogsAsync(string? entityType, int? entityId, int? userId, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 50)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }
        
        if (entityId.HasValue)
        {
            query = query.Where(a => a.EntityId == entityId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(a => a.ActorUserId == userId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value.ToUniversalTime());
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value.AddDays(1).ToUniversalTime());
        }

        var total = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AuditLog>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
