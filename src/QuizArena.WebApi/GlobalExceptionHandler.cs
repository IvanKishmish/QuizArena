using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace QuizArena.WebApi;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(exception, "Unhandled exception occured: {Message}", exception.Message);
        
        (int statusCode, string title) = exception switch
        {
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled"),
            ArgumentNullException => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };
        
        string detail = environment.IsDevelopment()
            ? exception.ToString()
            : statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = httpContext.Request.Path
        };
        
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}