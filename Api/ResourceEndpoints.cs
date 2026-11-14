using System.Security.Claims;
using MarksApi.Contracts;
using MarksApi.Services;

namespace MarksApi.Api;

internal static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", Login)
            .WithName("Login")
            .WithTags("Authentication")
            .WithSummary("Authenticate and issue a JWT.")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("login");

        var students = app.MapGroup("/api/students")
            .WithTags("Students")
            .RequireAuthorization();
        students.MapGet("", ListStudents)
            .WithName("ListStudents")
            .WithSummary("List students with paging, search, and sorting.")
            .Produces<PagedResponse<StudentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapGet("/{id:int}", GetStudent)
            .WithName("GetStudent")
            .WithSummary("Get a student by ID; students may access only their own record.")
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        students.MapPost("", CreateStudent)
            .WithName("CreateStudent")
            .WithSummary("Create a student.")
            .Produces<StudentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapPut("/{id:int}", UpdateStudent)
            .WithName("UpdateStudent")
            .WithSummary("Replace a student's editable information.")
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapDelete("/{id:int}", DeleteStudent)
            .WithName("DeleteStudent")
            .WithSummary("Delete a student who has no enrollments.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapGet("/{id:int}/transcript", GetTranscript)
            .WithName("GetStudentTranscript")
            .WithSummary("Get a student's transcript and credit-hour weighted average.")
            .Produces<TranscriptResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var courses = app.MapGroup("/api/courses")
            .WithTags("Courses")
            .RequireAuthorization();
        courses.MapGet("", ListCourses)
            .WithName("ListCourses")
            .WithSummary("List courses with paging, search, and sorting.")
            .Produces<PagedResponse<CourseResponse>>();
        courses.MapGet("/{id:int}", GetCourse)
            .WithName("GetCourse")
            .WithSummary("Get a course by ID.")
            .Produces<CourseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        courses.MapPost("", CreateCourse)
            .WithName("CreateCourse")
            .WithSummary("Create a course.")
            .Produces<CourseResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapPut("/{id:int}", UpdateCourse)
            .WithName("UpdateCourse")
            .WithSummary("Replace a course's editable information.")
            .Produces<CourseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapDelete("/{id:int}", DeleteCourse)
            .WithName("DeleteCourse")
            .WithSummary("Delete a course that has no enrollments.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapPost("/{courseId:int}/enrollments", CreateEnrollment)
            .WithName("EnrollStudentInCourse")
            .WithSummary("Enroll a student in a course.")
            .Produces<EnrollmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapGet("/{courseId:int}/enrollments", ListCourseEnrollments)
            .WithName("ListCourseEnrollments")
            .WithSummary("List enrollments in a course.")
            .Produces<PagedResponse<EnrollmentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapGet("/{courseId:int}/stats", GetCourseStats)
            .WithName("GetCourseStatistics")
            .WithSummary("Get enrollment, grading, average, and pass statistics for a course.")
            .Produces<CourseStatsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));

        var enrollments = app.MapGroup("/api/enrollments")
            .WithTags("Enrollments")
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        enrollments.MapGet("/{id:int}", GetEnrollment)
            .WithName("GetEnrollment")
            .WithSummary("Get an enrollment by ID.")
            .Produces<EnrollmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        enrollments.MapPut("/{id:int}/mark", UpdateMark)
            .WithName("UpdateEnrollmentMark")
            .WithSummary("Set an enrollment mark from 0 to 100.")
            .Produces<EnrollmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        enrollments.MapDelete("/{id:int}", DeleteEnrollment)
            .WithName("DeleteEnrollment")
            .WithSummary("Remove an enrollment.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var response = await service.LoginAsync(request, cancellationToken);
        return response is null
            ? Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The username or password is incorrect.")
            : Results.Ok(response);
    }

    private static async Task<IResult> ListStudents(
        IMarksService service,
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListStudentsAsync(page, pageSize, search, sort, cancellationToken));

    private static async Task<IResult> GetStudent(
        int id,
        ClaimsPrincipal principal,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        if (!CanReadStudent(principal, id))
        {
            return ApiResults.Forbidden("You may only read your own student record.");
        }
        var student = await service.GetStudentAsync(id, cancellationToken);
        return student is null
            ? ApiResults.NotFound($"Student {id} was not found.")
            : Results.Ok(student);
    }

    private static async Task<IResult> CreateStudent(
        CreateStudentRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var student = await service.CreateStudentAsync(request, cancellationToken);
        return Results.Created($"/api/students/{student.Id}", student);
    }

    private static async Task<IResult> UpdateStudent(
        int id,
        UpdateStudentRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var student = await service.UpdateStudentAsync(id, request, cancellationToken);
        return student is null
            ? ApiResults.NotFound($"Student {id} was not found.")
            : Results.Ok(student);
    }

    private static async Task<IResult> DeleteStudent(
        int id,
        IMarksService service,
        CancellationToken cancellationToken) =>
        await service.DeleteStudentAsync(id, cancellationToken)
            ? Results.NoContent()
            : ApiResults.NotFound($"Student {id} was not found.");

    private static async Task<IResult> GetTranscript(
        int id,
        ClaimsPrincipal principal,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        if (!CanReadStudent(principal, id))
        {
            return ApiResults.Forbidden("You may only read your own transcript.");
        }
        var transcript = await service.GetTranscriptAsync(id, cancellationToken);
        return transcript is null
            ? ApiResults.NotFound($"Student {id} was not found.")
            : Results.Ok(transcript);
    }

    private static async Task<IResult> ListCourses(
        IMarksService service,
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListCoursesAsync(page, pageSize, search, sort, cancellationToken));

    private static async Task<IResult> GetCourse(
        int id,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var course = await service.GetCourseAsync(id, cancellationToken);
        return course is null
            ? ApiResults.NotFound($"Course {id} was not found.")
            : Results.Ok(course);
    }

    private static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var course = await service.CreateCourseAsync(request, cancellationToken);
        return Results.Created($"/api/courses/{course.Id}", course);
    }

    private static async Task<IResult> UpdateCourse(
        int id,
        UpdateCourseRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var course = await service.UpdateCourseAsync(id, request, cancellationToken);
        return course is null
            ? ApiResults.NotFound($"Course {id} was not found.")
            : Results.Ok(course);
    }

    private static async Task<IResult> DeleteCourse(
        int id,
        IMarksService service,
        CancellationToken cancellationToken) =>
        await service.DeleteCourseAsync(id, cancellationToken)
            ? Results.NoContent()
            : ApiResults.NotFound($"Course {id} was not found.");

    private static async Task<IResult> CreateEnrollment(
        int courseId,
        CreateEnrollmentRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var enrollment = await service.CreateEnrollmentAsync(courseId, request, cancellationToken);
        return Results.Created($"/api/enrollments/{enrollment.Id}", enrollment);
    }

    private static async Task<IResult> ListCourseEnrollments(
        int courseId,
        IMarksService service,
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken)
    {
        var result = await service.ListCourseEnrollmentsAsync(
            courseId, page, pageSize, search, sort, cancellationToken);
        return result is null
            ? ApiResults.NotFound($"Course {courseId} was not found.")
            : Results.Ok(result);
    }

    private static async Task<IResult> GetEnrollment(
        int id,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var enrollment = await service.GetEnrollmentAsync(id, cancellationToken);
        return enrollment is null
            ? ApiResults.NotFound($"Enrollment {id} was not found.")
            : Results.Ok(enrollment);
    }

    private static async Task<IResult> UpdateMark(
        int id,
        UpdateMarkRequest request,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var enrollment = await service.UpdateMarkAsync(id, request, cancellationToken);
        return enrollment is null
            ? ApiResults.NotFound($"Enrollment {id} was not found.")
            : Results.Ok(enrollment);
    }

    private static async Task<IResult> DeleteEnrollment(
        int id,
        IMarksService service,
        CancellationToken cancellationToken) =>
        await service.DeleteEnrollmentAsync(id, cancellationToken)
            ? Results.NoContent()
            : ApiResults.NotFound($"Enrollment {id} was not found.");

    private static async Task<IResult> GetCourseStats(
        int courseId,
        IMarksService service,
        CancellationToken cancellationToken)
    {
        var stats = await service.GetCourseStatsAsync(courseId, cancellationToken);
        return stats is null
            ? ApiResults.NotFound($"Course {courseId} was not found.")
            : Results.Ok(stats);
    }

    private static bool CanReadStudent(ClaimsPrincipal principal, int studentId)
    {
        if (principal.IsInRole("Teacher"))
        {
            return true;
        }
        return principal.IsInRole("Student")
            && int.TryParse(principal.FindFirstValue("student_id"), out var ownStudentId)
            && ownStudentId == studentId;
    }
}
