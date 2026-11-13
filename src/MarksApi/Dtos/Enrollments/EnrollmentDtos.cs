using System.ComponentModel.DataAnnotations;

namespace MarksApi.Dtos.Enrollments;

public class CreateEnrollmentRequest
{
    [Required(ErrorMessage = "Student ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Student ID must be a positive integer.")]
    public int? StudentId { get; set; }
}

public class UpdateMarkRequest
{
    [Required(ErrorMessage = "Mark is required.")]
    [Range(0, 100, ErrorMessage = "Mark must be a whole number between 0 and 100.")]
    public int? Mark { get; set; }

    /// <summary>
    /// Optional optimistic concurrency version token (PostgreSQL xmin).
    /// When provided, if another teacher has updated the mark concurrently, a 409 Conflict is returned.
    /// </summary>
    public uint? Version { get; set; }
}

public class EnrollmentResponse
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string? StudentRegistrationNo { get; set; }
    public string? StudentFullName { get; set; }
    public int CourseId { get; set; }
    public string? CourseCode { get; set; }
    public int? Mark { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
    public uint Version { get; set; }
}

public class MarkResponse
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int? Mark { get; set; }
    public uint Version { get; set; }
}

