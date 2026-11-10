using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Api;

internal static class ApiResults
{
    public static IResult? Validate<T>(T request)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(
            request!,
            new ValidationContext(request!),
            validationResults,
            validateAllProperties: true);

        if (validationResults.Count == 0)
        {
            return null;
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => new
                {
                    Member = JsonNamingPolicy.CamelCase.ConvertName(member),
                    result.ErrorMessage
                }))
            .GroupBy(error => error.Member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage ?? "The value is invalid.").ToArray());

        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: "One or more validation errors occurred.",
            extensions: new Dictionary<string, object?> { ["errors"] = errors });
    }

    public static IResult NotFound(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: detail);

    public static IResult Conflict(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: detail);

    public static IResult Forbidden(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: detail);

    public static IResult InvalidSort(string parameter, string allowedValues) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: $"The sort value is invalid. Allowed values: {allowedValues}.",
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = new Dictionary<string, string[]>
                {
                    [parameter] = [$"Allowed values: {allowedValues}."]
                }
            });

    public static IResult ConstraintError(DbUpdateException exception)
    {
        var postgresException = exception.InnerException as PostgresException;
        return postgresException?.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => Conflict("A resource with the same unique value already exists."),
            PostgresErrorCodes.ForeignKeyViolation => Conflict("A referenced resource does not exist or cannot be removed."),
            PostgresErrorCodes.CheckViolation => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Error",
                detail: "One or more values violate a database constraint."),
            _ => throw exception
        };
    }

    public static bool IsConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.ForeignKeyViolation
                or PostgresErrorCodes.CheckViolation
        };
}

internal static class Pagination
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 50;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaximumPageSize);
        var maxPage = int.MaxValue / normalizedPageSize;
        var normalizedPage = Math.Clamp(page ?? 1, 1, maxPage);
        return (normalizedPage, normalizedPageSize);
    }
}
