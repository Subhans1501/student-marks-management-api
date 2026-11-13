namespace MarksApi.Data.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int? Mark { get; set; }
    public DateTime EnrolledAtUtc { get; set; }

    // PostgreSQL xmin system column used for optimistic concurrency control
    public uint Version { get; set; }

    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

