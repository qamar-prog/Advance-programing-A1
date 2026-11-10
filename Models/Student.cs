namespace MarksApi.Models;

public sealed class Student
{
    public int Id { get; set; }

    public required string RegistrationNo { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public ICollection<User> Users { get; set; } = new List<User>();
}