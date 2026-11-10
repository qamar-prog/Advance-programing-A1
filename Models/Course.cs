namespace MarksApi.Models;

public sealed class Course
{
    public int Id { get; set; }

    public required string CourseCode { get; set; }

    public required string Name { get; set; }

    public int CreditHours { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}