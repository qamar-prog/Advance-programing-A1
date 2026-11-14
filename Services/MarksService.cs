using System.Security.Claims;
using MarksApi.Api;
using MarksApi.Contracts;
using MarksApi.Data;
using MarksApi.Middleware;
using MarksApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

internal sealed class MarksService(
    AppDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<MarksService> logger) : IMarksService
{
    public async Task<bool> CheckDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        var healthy = await db.Database.CanConnectAsync(cancellationToken);
        if (!healthy)
        {
            logger.LogWarning("Health check failed because PostgreSQL is unavailable.");
        }
        return healthy;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Username == request.Username.Trim(), cancellationToken);
        if (user is null
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed
            || (user.Role == "Student" && user.StudentId is null))
        {
            logger.LogWarning("Login failed for username {Username}", request.Username);
            return null;
        }

        var issuer = RequiredConfiguration("Jwt:Issuer");
        var audience = RequiredConfiguration("Jwt:Audience");
        var key = RequiredConfiguration("Jwt:Key");
        var lifetime = configuration.GetValue<int>("Jwt:LifetimeMinutes");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(lifetime);
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
            expires: expiresAt.UtcDateTime,
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                JwtSecurity.Key(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        var encodedToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        logger.LogInformation("User {UserId} logged in with role {Role}", user.Id, user.Role);
        return new LoginResponse(encodedToken, expiresAt, user.Role);
    }

    public async Task<PagedResponse<StudentResponse>> ListStudentsAsync(
        int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken)
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
            _ => throw ApiServiceException.Validation("sort", "Allowed values: id, registrationNo, fullName, email, createdAtUtc.")
        };
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ordered.Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(student => new StudentResponse(
                student.Id, student.RegistrationNo, student.FullName, student.Email, student.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResponse<StudentResponse>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<StudentResponse?> GetStudentAsync(int id, CancellationToken cancellationToken) =>
        await db.Students.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new StudentResponse(
                item.Id, item.RegistrationNo, item.FullName, item.Email, item.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<StudentResponse> CreateStudentAsync(
        CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var student = new Student
        {
            RegistrationNo = request.RegistrationNo.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Students.Add(student);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created student {StudentId}", student.Id);
        return ToResponse(student);
    }

    public async Task<StudentResponse?> UpdateStudentAsync(
        int id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var student = await db.Students.FindAsync([id], cancellationToken);
        if (student is null)
        {
            return null;
        }

        student.RegistrationNo = request.RegistrationNo.Trim();
        student.FullName = request.FullName.Trim();
        student.Email = request.Email.Trim().ToLowerInvariant();
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated student {StudentId}", student.Id);
        return ToResponse(student);
    }

    public async Task<bool> DeleteStudentAsync(int id, CancellationToken cancellationToken)
    {
        var student = await db.Students.FindAsync([id], cancellationToken);
        if (student is null)
        {
            return false;
        }
        if (await db.Enrollments.AnyAsync(item => item.StudentId == id, cancellationToken))
        {
            throw ApiServiceException.Conflict("A student with enrollments cannot be deleted.");
        }

        var linkedUsers = await db.Users
            .Where(user => user.StudentId == id && user.Role == "Student")
            .ToListAsync(cancellationToken);
        db.Users.RemoveRange(linkedUsers);
        db.Students.Remove(student);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted student {StudentId}", id);
        return true;
    }

    public async Task<TranscriptResponse?> GetTranscriptAsync(int id, CancellationToken cancellationToken)
    {
        var student = await db.Students.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.RegistrationNo, item.FullName })
            .SingleOrDefaultAsync(cancellationToken);
        if (student is null)
        {
            return null;
        }

        var passMark = configuration.GetValue<int>("PassMark");
        var courses = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentId == id)
            .OrderBy(enrollment => enrollment.Course.Code)
            .Select(enrollment => new TranscriptItemResponse(
                enrollment.Course.Code,
                enrollment.Course.CreditHours,
                enrollment.Mark,
                enrollment.Mark == null ? null : enrollment.Mark >= passMark))
            .ToListAsync(cancellationToken);
        var graded = courses.Where(course => course.Mark.HasValue).ToArray();
        decimal? weightedAverage = graded.Length == 0
            ? null
            : decimal.Round(
                graded.Sum(course => (decimal)course.Mark!.Value * course.CreditHours)
                / graded.Sum(course => course.CreditHours),
                2,
                MidpointRounding.AwayFromZero);
        return new TranscriptResponse(
            student.Id, student.RegistrationNo, student.FullName, courses, weightedAverage);
    }

    public async Task<PagedResponse<CourseResponse>> ListCoursesAsync(
        int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken)
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
            _ => throw ApiServiceException.Validation("sort", "Allowed values: id, code, title, creditHours, createdAtUtc.")
        };
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ordered.Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(course => new CourseResponse(
                course.Id, course.Code, course.Title, course.CreditHours, course.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResponse<CourseResponse>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<CourseResponse?> GetCourseAsync(int id, CancellationToken cancellationToken) =>
        await db.Courses.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CourseResponse(
                item.Id, item.Code, item.Title, item.CreditHours, item.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CourseResponse> CreateCourseAsync(
        CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = new Course
        {
            Code = request.Code.Trim(),
            Title = request.Title.Trim(),
            CreditHours = request.CreditHours,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created course {CourseId}", course.Id);
        return ToResponse(course);
    }

    public async Task<CourseResponse?> UpdateCourseAsync(
        int id, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = await db.Courses.FindAsync([id], cancellationToken);
        if (course is null)
        {
            return null;
        }
        course.Code = request.Code.Trim();
        course.Title = request.Title.Trim();
        course.CreditHours = request.CreditHours;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated course {CourseId}", id);
        return ToResponse(course);
    }

    public async Task<bool> DeleteCourseAsync(int id, CancellationToken cancellationToken)
    {
        var course = await db.Courses.FindAsync([id], cancellationToken);
        if (course is null)
        {
            return false;
        }
        if (await db.Enrollments.AnyAsync(item => item.CourseId == id, cancellationToken))
        {
            throw ApiServiceException.Conflict("A course with enrollments cannot be deleted.");
        }
        db.Courses.Remove(course);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted course {CourseId}", id);
        return true;
    }

    public async Task<EnrollmentResponse> CreateEnrollmentAsync(
        int courseId, CreateEnrollmentRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            throw ApiServiceException.NotFound($"Course {courseId} was not found.");
        }
        if (!await db.Students.AnyAsync(student => student.Id == request.StudentId, cancellationToken))
        {
            throw ApiServiceException.NotFound($"Student {request.StudentId} was not found.");
        }

        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            CourseId = courseId,
            Mark = request.Mark,
            EnrolledAtUtc = DateTimeOffset.UtcNow
        };
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Created enrollment {EnrollmentId} for student {StudentId} in course {CourseId}",
            enrollment.Id, enrollment.StudentId, enrollment.CourseId);
        return ToResponse(enrollment);
    }

    public async Task<PagedResponse<EnrollmentResponse>?> ListCourseEnrollmentsAsync(
        int courseId, int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return null;
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
            _ => throw ApiServiceException.Validation("sort", "Allowed values: id, studentId, mark, enrolledAtUtc.")
        };
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ordered.Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(enrollment => new EnrollmentResponse(
                enrollment.Id, enrollment.StudentId, enrollment.CourseId,
                enrollment.Mark, enrollment.EnrolledAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResponse<EnrollmentResponse>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<EnrollmentResponse?> GetEnrollmentAsync(int id, CancellationToken cancellationToken) =>
        await db.Enrollments.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new EnrollmentResponse(
                item.Id, item.StudentId, item.CourseId, item.Mark, item.EnrolledAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<EnrollmentResponse?> UpdateMarkAsync(
        int id, UpdateMarkRequest request, CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments.FindAsync([id], cancellationToken);
        if (enrollment is null)
        {
            return null;
        }
        enrollment.Mark = request.Mark!.Value;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated mark for enrollment {EnrollmentId}", id);
        return ToResponse(enrollment);
    }

    public async Task<bool> DeleteEnrollmentAsync(int id, CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments.FindAsync([id], cancellationToken);
        if (enrollment is null)
        {
            return false;
        }
        db.Enrollments.Remove(enrollment);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted enrollment {EnrollmentId}", id);
        return true;
    }

    public async Task<CourseStatsResponse?> GetCourseStatsAsync(int courseId, CancellationToken cancellationToken)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(item => item.Id == courseId)
            .Select(item => new { item.Id, item.Code })
            .SingleOrDefaultAsync(cancellationToken);
        if (course is null)
        {
            return null;
        }
        var marks = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.CourseId == courseId)
            .Select(enrollment => enrollment.Mark)
            .ToListAsync(cancellationToken);
        var graded = marks.Where(mark => mark.HasValue).Select(mark => mark!.Value).ToArray();
        var passMark = configuration.GetValue<int>("PassMark");
        return new CourseStatsResponse(
            course.Id,
            course.Code,
            marks.Count,
            graded.Length,
            graded.Length == 0
                ? null
                : decimal.Round(graded.Sum(mark => (decimal)mark) / graded.Length, 2, MidpointRounding.AwayFromZero),
            graded.Length == 0 ? null : graded.Max(),
            graded.Length == 0 ? null : graded.Min(),
            graded.Count(mark => mark >= passMark));
    }

    private string RequiredConfiguration(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"Configuration value {key} is required.");

    private static StudentResponse ToResponse(Student student) =>
        new(student.Id, student.RegistrationNo, student.FullName, student.Email, student.CreatedAtUtc);

    private static CourseResponse ToResponse(Course course) =>
        new(course.Id, course.Code, course.Title, course.CreditHours, course.CreatedAtUtc);

    private static EnrollmentResponse ToResponse(Enrollment enrollment) =>
        new(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.Mark, enrollment.EnrolledAtUtc);
}
