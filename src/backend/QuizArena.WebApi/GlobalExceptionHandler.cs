using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using StackExchange.Redis;

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
        if (httpContext.Response.HasStarted)
            return false;

        logger.LogError(exception, "Unhandled exception occured: {Message}", exception.Message);

        (int statusCode, string title) = exception switch
        {
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled"),

            ArgumentNullException => (StatusCodes.Status400BadRequest, "Bad Request"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Validation Failed"),

            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),

            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency Conflict"),
            DbUpdateException dbEx when IsUniqueConstraintViolation(dbEx)
                => (StatusCodes.Status409Conflict, "Conflict"),
            DbUpdateException => (StatusCodes.Status500InternalServerError, "Database Update Failed"),

            MongoWriteException => (StatusCodes.Status409Conflict, "Conflict"),
            MongoConnectionException => (StatusCodes.Status503ServiceUnavailable, "Database Unavailable"),
            MongoException => (StatusCodes.Status503ServiceUnavailable, "Database Error"),

            RedisConnectionException => (StatusCodes.Status503ServiceUnavailable, "Cache Unavailable"),
            RedisTimeoutException => (StatusCodes.Status504GatewayTimeout, "Cache Timeout"),
            RedisException => (StatusCodes.Status503ServiceUnavailable, "Cache Error"),

            AuthenticationException => (StatusCodes.Status502BadGateway, "Email Provider Authentication Failed"),
            SmtpCommandException => (StatusCodes.Status502BadGateway, "Email Delivery Failed"),
            SmtpProtocolException => (StatusCodes.Status502BadGateway, "Email Delivery Failed"),

            TimeoutException => (StatusCodes.Status504GatewayTimeout, "Timeout"),
            NotSupportedException => (StatusCodes.Status400BadRequest, "Not Supported"),

            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var detail = environment.IsDevelopment()
            ? exception.ToString()
            : exception switch
            {
                FluentValidation.ValidationException or ArgumentException => exception.Message,
                _ => GenericDetailFor(statusCode)
            };

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
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static string GenericDetailFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "The request was invalid.",
        StatusCodes.Status401Unauthorized => "Authentication is required.",
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status409Conflict => "The request could not be completed due to a conflict.",
        StatusCodes.Status499ClientClosedRequest => "The request was cancelled.",
        >= 500 and < 600 => "An unexpected error occurred. Please try again later.",
        _ => "The request could not be completed."
    };

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
