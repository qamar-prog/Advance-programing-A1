using System.Security.Claims;
using MarksApi.Contracts;
using MarksApi.Data;
using MarksApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Api;

internal static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", Login);

        var students = app.MapGroup("/api/students").RequireAuthorization();
        students.MapGet("", ListStudents).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapGet("/{id:int}", GetStudent);
        students.MapPost("", CreateStudent).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapPut("/{id:int}", UpdateStudent).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapDelete("/{id:int}", DeleteStudent).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        students.MapGet("/{id:int}/transcript", GetTranscript);

        var courses = app.MapGroup("/api/courses").RequireAuthorization();
        courses.MapGet("", ListCourses);
        courses.MapGet("/{id:int}", GetCourse);
        courses.MapPost("", CreateCourse).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapPut("/{id:int}", UpdateCourse).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapDelete("/{id:int}", DeleteCourse).RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapPost("/{courseId:int}/enrollments", CreateEnrollment)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapGet("/{courseId:int}/enrollments", ListCourseEnrollments)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        courses.MapGet("/{courseId:int}/stats", GetCourseStats)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));

        var enrollments = app.MapGroup("/api/enrollments")
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        enrollments.MapGet("/{id:int}", GetEnrollment);
        enrollments.MapPut("/{id:int}/mark", UpdateMark);
        enrollments.MapDelete("/{id:int}", DeleteEnrollment);
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        AppDbContext db,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Username == request.Username.Trim(),
                cancellationToken);
        if (user is null
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed
            || (user.Role == "Student" && user.StudentId is null))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The username or password is incorrect.");
        }

        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Configuration value Jwt:Issuer is required.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Configuration value Jwt:Audience is required.");
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Secret configuration value Jwt:Key is required.");
        var lifetime = configuration.GetValue<int>("Jwt:LifetimeMinutes");
        var expiresAt = DateTime.UtcNow.AddMinutes(lifetime);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.StudentId.HasValue)
        {
            claims.Add(new Claim("student_id", user.StudentId.Value.ToString()));
        }

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                JwtSecurity.Key(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        var encodedToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        return Results.Ok(new LoginResponse(encodedToken, "Bearer", expiresAt));
    }

    private static async Task<IResult> ListStudents(
        AppDbContext db,
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var query = db.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(student =>
                EF.Functions.ILike(student.RegistrationNo, term)
                || EF.Functions.ILike(student.FullName, term)
                || EF.Functions.ILike(student.Email, term));
        }

        var ordered = sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "id" => query.OrderBy(student => student.Id),
            "registrationno" or "registration_no" => query.OrderBy(student => student.RegistrationNo),
            "fullname" or "full_name" => query.OrderBy(student => student.FullName),
            "email" => query.OrderBy(student => student.Email),
            "createdatutc" or "created_at_utc" => query.OrderBy(student => student.CreatedAtUtc),
            _ => null
        };
        if (ordered is null)
        {
            return ApiResults.InvalidSort("sort", "id, registrationNo, fullName, email, createdAtUtc");
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ordered
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(student => new StudentResponse(
                student.Id, student.RegistrationNo, student.FullName, student.Email, student.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<StudentResponse>(
            items, normalizedPage, normalizedPageSize, totalCount));
    }

    private static async Task<IResult> GetStudent(
        int id,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanReadStudent(principal, id))
        {
            return ApiResults.Forbidden("You may only read your own student record.");
        }

        var student = await db.Students.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new StudentResponse(
                item.Id, item.RegistrationNo, item.FullName, item.Email, item.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return student is null
            ? ApiResults.NotFound($"Student {id} was not found.")
            : Results.Ok(student);
    }

    private static async Task<IResult> CreateStudent(
        CreateStudentRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var student = new Student
        {
            RegistrationNo = request.RegistrationNo.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Students.Add(student);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }

        var response = ToStudentResponse(student);
        return Results.Created($"/api/students/{student.Id}", response);
    }

    private static async Task<IResult> UpdateStudent(
        int id,
        UpdateStudentRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var student = await db.Students.FindAsync([id], cancellationToken);
        if (student is null)
        {
            return ApiResults.NotFound($"Student {id} was not found.");
        }

        student.RegistrationNo = request.RegistrationNo.Trim();
        student.FullName = request.FullName.Trim();
        student.Email = request.Email.Trim().ToLowerInvariant();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }

        return Results.Ok(ToStudentResponse(student));
    }

    private static async Task<IResult> DeleteStudent(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var student = await db.Students.FindAsync([id], cancellationToken);
        if (student is null)
        {
            return ApiResults.NotFound($"Student {id} was not found.");
        }

        if (await db.Enrollments.AnyAsync(item => item.StudentId == id, cancellationToken)
            || await db.Users.AnyAsync(user => user.StudentId == id, cancellationToken))
        {
            return ApiResults.Conflict("A student with enrollments or a linked user cannot be deleted.");
        }

        db.Students.Remove(student);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> GetTranscript(
        int id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!CanReadStudent(principal, id))
        {
            return ApiResults.Forbidden("You may only read your own transcript.");
        }

        var student = await db.Students.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.RegistrationNo, item.FullName })
            .SingleOrDefaultAsync(cancellationToken);
        if (student is null)
        {
            return ApiResults.NotFound($"Student {id} was not found.");
        }

        var passMark = configuration.GetValue<int>("PassMark");
        var courses = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentId == id)
            .OrderBy(enrollment => enrollment.Course.Code)
            .Select(enrollment => new TranscriptItemResponse(
                enrollment.CourseId,
                enrollment.Course.Code,
                enrollment.Course.Title,
                enrollment.Course.CreditHours,
                enrollment.Mark,
                enrollment.Mark == null ? null : enrollment.Mark >= passMark))
            .ToListAsync(cancellationToken);
        var graded = courses.Where(course => course.Mark.HasValue).ToArray();
        decimal? weightedAverage = graded.Length == 0
            ? null
            : graded.Sum(course => (decimal)course.Mark!.Value * course.CreditHours)
                / graded.Sum(course => course.CreditHours);

        return Results.Ok(new TranscriptResponse(
            student.Id, student.RegistrationNo, student.FullName, courses, weightedAverage));
    }

    private static async Task<IResult> ListCourses(
        AppDbContext db,
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var query = db.Courses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(course =>
                EF.Functions.ILike(course.Code, term)
                || EF.Functions.ILike(course.Title, term));
        }

        var ordered = sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "id" => query.OrderBy(course => course.Id),
            "code" => query.OrderBy(course => course.Code),
            "title" => query.OrderBy(course => course.Title),
            "credithours" or "credit_hours" => query.OrderBy(course => course.CreditHours),
            "createdatutc" or "created_at_utc" => query.OrderBy(course => course.CreatedAtUtc),
            _ => null
        };
        if (ordered is null)
        {
            return ApiResults.InvalidSort("sort", "id, code, title, creditHours, createdAtUtc");
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ordered
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(course => new CourseResponse(
                course.Id, course.Code, course.Title, course.CreditHours, course.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<CourseResponse>(
            items, normalizedPage, normalizedPageSize, totalCount));
    }

    private static async Task<IResult> GetCourse(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CourseResponse(
                item.Id, item.Code, item.Title, item.CreditHours, item.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return course is null
            ? ApiResults.NotFound($"Course {id} was not found.")
            : Results.Ok(course);
    }

    private static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var course = new Course
        {
            Code = request.Code.Trim(),
            Title = request.Title.Trim(),
            CreditHours = request.CreditHours,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Courses.Add(course);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }

        return Results.Created($"/api/courses/{course.Id}", ToCourseResponse(course));
    }

    private static async Task<IResult> UpdateCourse(
        int id,
        UpdateCourseRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var course = await db.Courses.FindAsync([id], cancellationToken);
        if (course is null)
        {
            return ApiResults.NotFound($"Course {id} was not found.");
        }

        course.Code = request.Code.Trim();
        course.Title = request.Title.Trim();
        course.CreditHours = request.CreditHours;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }

        return Results.Ok(ToCourseResponse(course));
    }

    private static async Task<IResult> DeleteCourse(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var course = await db.Courses.FindAsync([id], cancellationToken);
        if (course is null)
        {
            return ApiResults.NotFound($"Course {id} was not found.");
        }
        if (await db.Enrollments.AnyAsync(item => item.CourseId == id, cancellationToken))
        {
            return ApiResults.Conflict("A course with enrollments cannot be deleted.");
        }

        db.Courses.Remove(course);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> CreateEnrollment(
        int courseId,
        CreateEnrollmentRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return ApiResults.NotFound($"Course {courseId} was not found.");
        }
        if (!await db.Students.AnyAsync(student => student.Id == request.StudentId, cancellationToken))
        {
            return ApiResults.NotFound($"Student {request.StudentId} was not found.");
        }

        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            CourseId = courseId,
            Mark = request.Mark,
            EnrolledAtUtc = DateTimeOffset.UtcNow
        };
        db.Enrollments.Add(enrollment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ApiResults.IsConstraintViolation(exception))
        {
            return ApiResults.ConstraintError(exception);
        }

        return Results.Created(
            $"/api/enrollments/{enrollment.Id}",
            ToEnrollmentResponse(enrollment));
    }

    private static async Task<IResult> ListCourseEnrollments(
        int courseId,
        AppDbContext db,
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return ApiResults.NotFound($"Course {courseId} was not found.");
        }
        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var query = db.Enrollments.AsNoTracking().Where(item => item.CourseId == courseId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(enrollment =>
                EF.Functions.ILike(enrollment.Student.RegistrationNo, term)
                || EF.Functions.ILike(enrollment.Student.FullName, term));
        }

        var ordered = sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "id" => query.OrderBy(enrollment => enrollment.Id),
            "studentid" or "student_id" => query.OrderBy(enrollment => enrollment.StudentId),
            "mark" => query.OrderBy(enrollment => enrollment.Mark),
            "enrolledatutc" or "enrolled_at_utc" => query.OrderBy(enrollment => enrollment.EnrolledAtUtc),
            _ => null
        };
        if (ordered is null)
        {
            return ApiResults.InvalidSort("sort", "id, studentId, mark, enrolledAtUtc");
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ordered
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(enrollment => new EnrollmentResponse(
                enrollment.Id, enrollment.StudentId, enrollment.CourseId,
                enrollment.Mark, enrollment.EnrolledAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<EnrollmentResponse>(
            items, normalizedPage, normalizedPageSize, totalCount));
    }

    private static async Task<IResult> UpdateMark(
        int id,
        UpdateMarkRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ApiResults.Validate(request);
        if (validation is not null)
        {
            return validation;
        }
        var enrollment = await db.Enrollments.FindAsync([id], cancellationToken);
        if (enrollment is null)
        {
            return ApiResults.NotFound($"Enrollment {id} was not found.");
        }
        enrollment.Mark = request.Mark!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToEnrollmentResponse(enrollment));
    }

    private static async Task<IResult> GetEnrollment(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new EnrollmentResponse(
                item.Id, item.StudentId, item.CourseId, item.Mark, item.EnrolledAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return enrollment is null
            ? ApiResults.NotFound($"Enrollment {id} was not found.")
            : Results.Ok(enrollment);
    }

    private static async Task<IResult> DeleteEnrollment(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments.FindAsync([id], cancellationToken);
        if (enrollment is null)
        {
            return ApiResults.NotFound($"Enrollment {id} was not found.");
        }
        db.Enrollments.Remove(enrollment);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCourseStats(
        int courseId,
        AppDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return ApiResults.NotFound($"Course {courseId} was not found.");
        }

        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.CourseId == courseId)
            .Select(enrollment => enrollment.Mark)
            .ToListAsync(cancellationToken);
        var graded = enrollments.Where(mark => mark.HasValue).Select(mark => mark!.Value).ToArray();
        var passMark = configuration.GetValue<int>("PassMark");
        var response = new CourseStatsResponse(
            courseId,
            enrollments.Count,
            graded.Length,
            graded.Length == 0 ? null : (decimal)graded.Average(),
            graded.Count(mark => mark >= passMark),
            graded.Count(mark => mark < passMark));
        return Results.Ok(response);
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

    private static StudentResponse ToStudentResponse(Student student) =>
        new(student.Id, student.RegistrationNo, student.FullName, student.Email, student.CreatedAtUtc);

    private static CourseResponse ToCourseResponse(Course course) =>
        new(course.Id, course.Code, course.Title, course.CreditHours, course.CreatedAtUtc);

    private static EnrollmentResponse ToEnrollmentResponse(Enrollment enrollment) =>
        new(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.Mark, enrollment.EnrolledAtUtc);
}
