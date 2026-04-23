using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = Roles.Admin)]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? entityType, [FromQuery] int? entityId, [FromQuery] int? userId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _auditService.GetAuditLogsAsync(entityType, entityId, userId, fromDate, toDate, page, pageSize);
        return Ok(new { success = true, data = result });
    }
}
