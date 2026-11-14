using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, errors) = exception switch
        {
            ApiServiceException apiException =>
                (apiException.StatusCode, apiException.Title, apiException.Message, apiException.Errors),
            DbUpdateException updateException when FindPostgresException(updateException) is { } postgresException =>
                ConstraintProblem(postgresException),
            _ => (0, string.Empty, string.Empty, (IDictionary<string, string[]>?)null)
        };

        if (statusCode == 0)
        {
            logger.LogError(exception, "Unhandled API exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            return false;
        }

        if (exception is DbUpdateException)
        {
            var postgresException = FindPostgresException(exception);
            logger.LogWarning(
                "Database constraint rejected {Method} {Path}; SQL state {SqlState}, constraint {ConstraintName}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                postgresException?.SqlState,
                postgresException?.ConstraintName);
        }
        else
        {
            logger.LogInformation("API request returned {StatusCode} for {Method} {Path}: {Detail}",
                statusCode, httpContext.Request.Method, httpContext.Request.Path, detail);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int, string, string, IDictionary<string, string[]>?) ConstraintProblem(
        PostgresException exception) =>
        exception.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation =>
                (StatusCodes.Status409Conflict, "Conflict",
                    "A resource with the same unique value already exists.", null),
            PostgresErrorCodes.ForeignKeyViolation =>
                (StatusCodes.Status409Conflict, "Conflict",
                    "A referenced resource does not exist or cannot be removed.", null),
            PostgresErrorCodes.CheckViolation =>
                (StatusCodes.Status400BadRequest, "Validation Error",
                    "One or more values violate a database constraint.",
                    new Dictionary<string, string[]>
                    {
                        ["request"] = ["One or more values are outside the allowed range."]
                    }),
            _ => (0, string.Empty, string.Empty, null)
        };

    private static PostgresException? FindPostgresException(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
            current = current.InnerException;
        }
        return null;
    }
}

internal sealed class ApiServiceException(
    int statusCode,
    string title,
    string message,
    IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;

    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static ApiServiceException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, "Not Found", detail);

    public static ApiServiceException Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, "Conflict", detail);

    public static ApiServiceException Validation(string field, string detail) =>
        new(StatusCodes.Status400BadRequest, "Validation Error", detail,
            new Dictionary<string, string[]> { [field] = [detail] });
}
