using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LoanFlow.API.Middleware;

/// <summary>
/// Middleware that injects the current user's BranchId into HttpContext.Items
/// for downstream services to use for branch-level data isolation.
/// Admin and Manager(HQ) users with BranchId = null see all data.
/// </summary>
public class BranchFilterMiddleware
{
    private readonly RequestDelegate _next;

    public BranchFilterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, LoanFlowDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

            if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
            {
                // Admin always sees everything
                if (roleClaim == "Admin")
                {
                    context.Items["BranchId"] = (int?)null;
                    context.Items["BranchFilterEnabled"] = false;
                }
                else
                {
                    var user = await db.Users.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.BranchId })
                        .FirstOrDefaultAsync();

                    if (user?.BranchId != null)
                    {
                        context.Items["BranchId"] = user.BranchId;
                        context.Items["BranchFilterEnabled"] = true;
                    }
                    else
                    {
                        // HQ user (null branch) — sees everything
                        context.Items["BranchId"] = (int?)null;
                        context.Items["BranchFilterEnabled"] = false;
                    }
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods to read branch context from HttpContext.
/// </summary>
public static class BranchFilterExtensions
{
    public static int? GetBranchId(this HttpContext context)
        => context.Items.TryGetValue("BranchId", out var val) ? val as int? : null;

    public static bool IsBranchFilterEnabled(this HttpContext context)
        => context.Items.TryGetValue("BranchFilterEnabled", out var val) && val is true;
}
