using System.ComponentModel.DataAnnotations;

namespace MarksApi.Dtos.Courses;

public class CreateCourseRequest
{
    [Required(ErrorMessage = "Course code is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Course code must be between 2 and 10 characters.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Course title is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Course title must be between 2 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Credit hours is required.")]
    [Range(1, 4, ErrorMessage = "Credit hours must be between 1 and 4.")]
    public int? CreditHours { get; set; }
}

public class UpdateCourseRequest
{
    [Required(ErrorMessage = "Course code is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Course code must be between 2 and 10 characters.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Course title is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Course title must be between 2 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Credit hours is required.")]
    [Range(1, 4, ErrorMessage = "Credit hours must be between 1 and 4.")]
    public int? CreditHours { get; set; }
}

public class CourseResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

