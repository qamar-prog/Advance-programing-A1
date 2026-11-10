using MarksApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(student => student.Id);
            entity.Property(student => student.RegistrationNo)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(student => student.FullName)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(student => student.Email)
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(student => student.CreatedAtUtc)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(student => student.RegistrationNo)
                .IsUnique();
            entity.HasIndex(student => student.Email)
                .IsUnique();
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_students_email_lowercase",
                "email = lower(email)"));
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(course => course.Id);
            entity.Property(course => course.Code)
                .HasMaxLength(10)
                .IsRequired();
            entity.Property(course => course.Title)
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(course => course.CreatedAtUtc)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(course => course.Code)
                .IsUnique();
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_courses_credit_hours",
                "credit_hours BETWEEN 1 AND 4"));
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(enrollment => enrollment.Id);
            entity.Property(enrollment => enrollment.EnrolledAtUtc)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(enrollment => new { enrollment.StudentId, enrollment.CourseId })
                .IsUnique();
            entity.HasIndex(enrollment => enrollment.CourseId);
            entity.HasOne(enrollment => enrollment.Student)
                .WithMany(student => student.Enrollments)
                .HasForeignKey(enrollment => enrollment.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(enrollment => enrollment.Course)
                .WithMany(course => course.Enrollments)
                .HasForeignKey(enrollment => enrollment.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_enrollments_mark",
                "mark IS NULL OR mark BETWEEN 0 AND 100"));
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(user => user.PasswordHash)
                .HasColumnType("text")
                .IsRequired();
            entity.Property(user => user.Role)
                .HasMaxLength(20)
                .IsRequired();
            entity.HasIndex(user => user.Username)
                .IsUnique();
            entity.HasOne(user => user.Student)
                .WithMany(student => student.Users)
                .HasForeignKey(user => user.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_users_role",
                "role IN ('Teacher', 'Student')"));
        });
    }
}