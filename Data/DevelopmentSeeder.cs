using MarksApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MarksApi.Data;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        ILogger logger,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogWarning("Development seed data was not created because PostgreSQL is unavailable.");
            return;
        }

        var existingCounts = new[]
        {
            await db.Students.CountAsync(cancellationToken),
            await db.Courses.CountAsync(cancellationToken),
            await db.Users.CountAsync(cancellationToken),
            await db.Enrollments.CountAsync(cancellationToken)
        };
        if (existingCounts.Any(count => count != 0))
        {
            logger.LogInformation("Development seed data skipped because the database is not empty.");
            return;
        }

        var teacherUsername = RequiredSetting(configuration, "DemoAccounts:TeacherUsername");
        var teacherPassword = RequiredSetting(configuration, "DemoAccounts:TeacherPassword");
        var student1Username = RequiredSetting(configuration, "DemoAccounts:Student1Username");
        var student1Password = RequiredSetting(configuration, "DemoAccounts:Student1Password");
        var student2Username = RequiredSetting(configuration, "DemoAccounts:Student2Username");
        var student2Password = RequiredSetting(configuration, "DemoAccounts:Student2Password");

        var students = new[]
        {
            new Student
            {
                RegistrationNo = "DEMO-0001",
                FullName = "Aisha Khan",
                Email = "aisha.khan@example.test",
                CreatedAtUtc = DateTimeOffset.UtcNow
            },
            new Student
            {
                RegistrationNo = "DEMO-0002",
                FullName = "Bilal Ahmed",
                Email = "bilal.ahmed@example.test",
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var courses = new[]
        {
            new Course
            {
                Code = "CS101",
                Title = "Programming Fundamentals",
                CreditHours = 3,
                CreatedAtUtc = DateTimeOffset.UtcNow
            },
            new Course
            {
                Code = "MATH101",
                Title = "Mathematics",
                CreditHours = 4,
                CreatedAtUtc = DateTimeOffset.UtcNow
            },
            new Course
            {
                Code = "DB101",
                Title = "Database Systems",
                CreditHours = 3,
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };

        db.Students.AddRange(students);
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync(cancellationToken);

        db.Enrollments.AddRange(
            NewEnrollment(students[0], courses[0], 82),
            NewEnrollment(students[0], courses[1], 76),
            NewEnrollment(students[0], courses[2], 91),
            NewEnrollment(students[1], courses[0], 67),
            NewEnrollment(students[1], courses[1], 48),
            NewEnrollment(students[1], courses[2], null));

        var teacher = new User
        {
            Username = teacherUsername,
            PasswordHash = string.Empty,
            Role = "Teacher"
        };
        var studentUser1 = new User
        {
            Username = student1Username,
            PasswordHash = string.Empty,
            Role = "Student",
            Student = students[0]
        };
        var studentUser2 = new User
        {
            Username = student2Username,
            PasswordHash = string.Empty,
            Role = "Student",
            Student = students[1]
        };
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        teacher.PasswordHash = passwordHasher.HashPassword(teacher, teacherPassword);
        studentUser1.PasswordHash = passwordHasher.HashPassword(studentUser1, student1Password);
        studentUser2.PasswordHash = passwordHasher.HashPassword(studentUser2, student2Password);
        db.Users.AddRange(teacher, studentUser1, studentUser2);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inserted development-only sample students, courses, enrollments, and users.");
    }

    private static string RequiredSetting(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Development seed configuration value {key} is required when the database is empty.");

    private static Enrollment NewEnrollment(Student student, Course course, int? mark) =>
        new()
        {
            Student = student,
            Course = course,
            Mark = mark,
            EnrolledAtUtc = DateTimeOffset.UtcNow
        };
}
