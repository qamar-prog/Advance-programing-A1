using System.ComponentModel.DataAnnotations;

namespace MarksApi.Contracts;

/// <summary>Fields required to register a student.</summary>
public sealed record CreateStudentRequest
{
    [Required, StringLength(20)]
    public string RegistrationNo { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Replacement values for an existing student.</summary>
public sealed record UpdateStudentRequest
{
    [Required, StringLength(20)]
    public string RegistrationNo { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Public student information returned by the API.</summary>
public sealed record StudentResponse(
    int Id,
    string RegistrationNo,
    string FullName,
    string Email,
    DateTimeOffset CreatedAtUtc);

/// <summary>Fields required to create a course.</summary>
public sealed record CreateCourseRequest
{
    [Required, StringLength(10)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string Title { get; init; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; init; }
}

/// <summary>Replacement values for an existing course.</summary>
public sealed record UpdateCourseRequest
{
    [Required, StringLength(10)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string Title { get; init; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; init; }
}

/// <summary>Public course information returned by the API.</summary>
public sealed record CourseResponse(
    int Id,
    string Code,
    string Title,
    int CreditHours,
    DateTimeOffset CreatedAtUtc);

/// <summary>Fields required to enroll a student in a course.</summary>
public sealed record CreateEnrollmentRequest
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; init; }

    [Range(0, 100)]
    public int? Mark { get; init; }
}

/// <summary>A mark from zero to one hundred.</summary>
public sealed record UpdateMarkRequest
{
    [Required]
    [Range(0, 100)]
    public int? Mark { get; init; }
}

/// <summary>An enrollment and its optional mark.</summary>
public sealed record EnrollmentResponse(
    int Id,
    int StudentId,
    int CourseId,
    int? Mark,
    DateTimeOffset EnrolledAtUtc);

/// <summary>Fields for creating a user account.</summary>
public sealed record CreateUserRequest
{
    [Required, StringLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required, RegularExpression("^(Teacher|Student)$")]
    public string Role { get; init; } = string.Empty;

    public int? StudentId { get; init; }
}

/// <summary>Credentials submitted for authentication.</summary>
public sealed record LoginRequest
{
    [Required, StringLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

/// <summary>A signed bearer token and its expiration.</summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAtUtc, string Role);

/// <summary>Public user information without password hash material.</summary>
public sealed record UserResponse(
    int Id,
    string Username,
    string Role,
    int? StudentId);

/// <summary>A page of results with paging metadata.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount);

/// <summary>Aggregate mark statistics for a course.</summary>
public sealed record CourseStatsResponse(
    int CourseId,
    string CourseCode,
    int Enrolled,
    int Graded,
    decimal? Average,
    int? Highest,
    int? Lowest,
    int Passed);

/// <summary>A graded or not-yet-graded course in a student's transcript.</summary>
public sealed record TranscriptItemResponse(
    string Code,
    int CreditHours,
    int? Mark,
    bool? Passed);

/// <summary>A student's courses and credit-hour weighted result.</summary>
public sealed record TranscriptResponse(
    int StudentId,
    string RegistrationNo,
    string FullName,
    IReadOnlyList<TranscriptItemResponse> Courses,
    decimal? WeightedAverage);
