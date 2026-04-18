using LoanFlow.Application.Common;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LoanFlow.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during the request.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        
        // Default to internal server error
        int statusCode = StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Server Error",
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Detail = "An unexpected error occurred while processing your request."
        };

        // Here we could add logic to catch Specific domain exceptions like InvalidOperationException or NotFoundException
        if (ex is ConcurrencyConflictException)
        {
            statusCode = StatusCodes.Status409Conflict;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Conflict";
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8";
            problemDetails.Detail = ex.Message;
        }
        else if (ex is InvalidOperationException || ex is ArgumentException)
        {
            statusCode = StatusCodes.Status400BadRequest;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Bad Request";
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1";
            problemDetails.Detail = ex.Message; // Safe to expose domain validation messages to client
        }
        else if (ex is UnauthorizedAccessException)
        {
            statusCode = StatusCodes.Status403Forbidden;
            problemDetails.Status = statusCode;
            problemDetails.Title = "Forbidden";
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3";
            problemDetails.Detail = "You do not have permission to perform this action.";
        }
        context.Response.StatusCode = statusCode;
        
        var result = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return context.Response.WriteAsync(result);
    }
}
