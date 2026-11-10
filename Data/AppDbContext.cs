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
            entity.Property(student => student.StudentNumber)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(student => student.FirstName)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(student => student.LastName)
                .HasMaxLength(100)
                .IsRequired();
            entity.HasIndex(student => student.StudentNumber)
                .IsUnique();
            entity.HasIndex(student => new { student.LastName, student.FirstName });
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(course => course.Id);
            entity.Property(course => course.CourseCode)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(course => course.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.HasIndex(course => course.CourseCode)
                .IsUnique();
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_courses_credit_hours",
                "credit_hours BETWEEN 1 AND 4"));
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(enrollment => new { enrollment.StudentId, enrollment.CourseId });
            entity.HasIndex(enrollment => enrollment.CourseId);
            entity.HasOne(enrollment => enrollment.Student)
                .WithMany(student => student.Enrollments)
                .HasForeignKey(enrollment => enrollment.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(enrollment => enrollment.Course)
                .WithMany(course => course.Enrollments)
                .HasForeignKey(enrollment => enrollment.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_enrollments_mark",
                "mark IS NULL OR mark BETWEEN 0 AND 100"));
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(user => user.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(user => user.Role)
                .HasMaxLength(30)
                .IsRequired();
            entity.HasIndex(user => user.Username)
                .IsUnique();
            entity.HasIndex(user => user.StudentId)
                .IsUnique();
            entity.HasOne(user => user.Student)
                .WithOne(student => student.User)
                .HasForeignKey<User>(user => user.StudentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_users_role",
                "role IN ('Teacher', 'Student')"));
        });
    }
}