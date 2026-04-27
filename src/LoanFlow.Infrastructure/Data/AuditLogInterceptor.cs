using LoanFlow.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LoanFlow.Infrastructure.Data;

public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        PopulateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        PopulateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void PopulateAuditFields(DbContext? context)
    {
        if (context == null) return;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
        var sessionId = httpContext.Request.Headers["X-Session-Id"].ToString();
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = httpContext.TraceIdentifier;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.IpAddress ??= ipAddress;
                entry.Entity.UserAgent ??= userAgent;
                entry.Entity.SessionId ??= sessionId;
                entry.Entity.CorrelationId ??= correlationId;
            }
        }
    }
}
