namespace MarksApi.Models;

public sealed class Student
{
    public int Id { get; set; }

    public required string StudentNumber { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public User? User { get; set; }
}