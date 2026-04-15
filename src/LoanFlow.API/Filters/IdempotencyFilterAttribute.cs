using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.API.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class IdempotencyFilterAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues))
        {
            // Enforcement: Require the header for this endpoint
            context.Result = new BadRequestObjectResult(new ProblemDetails 
            { 
                Title = "Missing Idempotency-Key", 
                Detail = "This operation requires an 'Idempotency-Key' header to prevent duplicate execution." 
            });
            return;
        }

        var key = idempotencyKeyValues.ToString();

        // Need the DbContext from services
        var dbContent = context.HttpContext.RequestServices.GetRequiredService<LoanFlowDbContext>();

        var existingRecord = await dbContent.IdempotencyRecords.FirstOrDefaultAsync(x => x.IdempotencyKey == key);

        if (existingRecord != null)
        {
            // The request was already processed
            context.Result = new ObjectResult(existingRecord.ResponseBody != null ? JsonSerializer.Deserialize<object>(existingRecord.ResponseBody) : null)
            {
                StatusCode = existingRecord.StatusCode
            };
            return;
        }

        // Create a pending record to lock it 
        var newRecord = new IdempotencyRecord
        {
            IdempotencyKey = key,
            CreatedAt = DateTime.UtcNow,
            StatusCode = StatusCodes.Status202Accepted
        };

        try 
        {
            dbContent.IdempotencyRecords.Add(newRecord);
            await dbContent.SaveChangesAsync();
        } 
        catch (DbUpdateException) 
        {
            // A race condition occurred and another request grabbed this key.
            context.Result = new ConflictObjectResult(new ProblemDetails { Detail = "A request with this Idempotency-Key is already being processed." });
            return;
        }

        var executedContext = await next();

        // Capture response
        if (executedContext.Exception == null && executedContext.Result is ObjectResult objResult)
        {
            newRecord.StatusCode = objResult.StatusCode ?? StatusCodes.Status200OK;
            newRecord.ResponseBody = JsonSerializer.Serialize(objResult.Value);
            dbContent.IdempotencyRecords.Update(newRecord);
            
            // Note: We use a new scope here because the main DbContext might be disposed or corrupted by exceptions
            using var scope = context.HttpContext.RequestServices.CreateScope();
            var db2 = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
            
            var recordToUpdate = await db2.IdempotencyRecords.FindAsync(key);
            if (recordToUpdate != null)
            {
                recordToUpdate.StatusCode = newRecord.StatusCode;
                recordToUpdate.ResponseBody = newRecord.ResponseBody;
                await db2.SaveChangesAsync();
            }
        }
        else if (executedContext.Exception != null)
        {
            // If it failed, delete the key so they can retry
            using var scope = context.HttpContext.RequestServices.CreateScope();
            var db2 = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
            var recordToDelete = await db2.IdempotencyRecords.FindAsync(key);
            if (recordToDelete != null)
            {
                db2.IdempotencyRecords.Remove(recordToDelete);
                await db2.SaveChangesAsync();
            }
        }
    }
}
