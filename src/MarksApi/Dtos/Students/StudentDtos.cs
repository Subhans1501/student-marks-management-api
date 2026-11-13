using System.ComponentModel.DataAnnotations;

namespace MarksApi.Dtos.Students;

public class CreateStudentRequest
{
    [Required(ErrorMessage = "Registration number is required.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Registration number must be between 2 and 20 characters.")]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Full name must be between 1 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}

public class UpdateStudentRequest
{
    [Required(ErrorMessage = "Registration number is required.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Registration number must be between 2 and 20 characters.")]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Full name must be between 1 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}

public class StudentResponse
{
    public int Id { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

