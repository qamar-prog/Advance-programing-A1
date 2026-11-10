using System.ComponentModel.DataAnnotations;

namespace MarksApi.Contracts;

public sealed class CreateStudentRequest
{
    [Required, StringLength(20)]
    public string RegistrationNo { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;
}

public sealed class UpdateStudentRequest
{
    [Required, StringLength(20)]
    public string RegistrationNo { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;
}

public sealed record StudentResponse(
    int Id,
    string RegistrationNo,
    string FullName,
    string Email,
    DateTimeOffset CreatedAtUtc);

public sealed class CreateCourseRequest
{
    [Required, StringLength(10)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string Title { get; init; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; init; }
}

public sealed class UpdateCourseRequest
{
    [Required, StringLength(10)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string Title { get; init; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; init; }
}

public sealed record CourseResponse(
    int Id,
    string Code,
    string Title,
    int CreditHours,
    DateTimeOffset CreatedAtUtc);

public sealed class CreateEnrollmentRequest
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; init; }

    [Range(0, 100)]
    public int? Mark { get; init; }
}

public sealed class UpdateMarkRequest
{
    [Required]
    [Range(0, 100)]
    public int? Mark { get; init; }
}

public sealed record EnrollmentResponse(
    int Id,
    int StudentId,
    int CourseId,
    int? Mark,
    DateTimeOffset EnrolledAtUtc);

public sealed class CreateUserRequest
{
    [Required, StringLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required, RegularExpression("^(Teacher|Student)$")]
    public string Role { get; init; } = string.Empty;

    public int? StudentId { get; init; }
}

public sealed class LoginRequest
{
    [Required, StringLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

public sealed record UserResponse(
    int Id,
    string Username,
    string Role,
    int? StudentId);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record CourseStatsResponse(
    int CourseId,
    int EnrollmentCount,
    int GradedCount,
    decimal? Average,
    int PassedCount,
    int FailedCount);

public sealed record TranscriptItemResponse(
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int CreditHours,
    int? Mark,
    bool? Passed);

public sealed record TranscriptResponse(
    int StudentId,
    string RegistrationNo,
    string FullName,
    IReadOnlyList<TranscriptItemResponse> Courses,
    decimal? WeightedAverage);
