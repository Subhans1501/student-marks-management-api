using System.Security.Claims;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Reports;
using MarksApi.Dtos.Students;
using MarksApi.Exceptions;
using MarksApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarksApi.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(PagedResponse<StudentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<StudentResponse>>> GetStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _studentService.GetStudentsAsync(page, pageSize, search, sort, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Teacher,Student")]
    [ProducesResponseType(typeof(StudentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResponse>> GetStudentById(
        int id,
        CancellationToken cancellationToken)
    {
        EnsureCanAccessStudentRecord(id);
        var student = await _studentService.GetStudentByIdAsync(id, cancellationToken);
        return Ok(student);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(StudentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentResponse>> CreateStudent(
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _studentService.CreateStudentAsync(request, cancellationToken);
        return Created($"/api/students/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(StudentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentResponse>> UpdateStudent(
        int id,
        [FromBody] UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _studentService.UpdateStudentAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteStudent(
        int id,
        CancellationToken cancellationToken)
    {
        await _studentService.DeleteStudentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/transcript")]
    [Authorize(Roles = "Teacher,Student")]
    [ProducesResponseType(typeof(TranscriptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptResponse>> GetTranscript(
        int id,
        CancellationToken cancellationToken)
    {
        EnsureCanAccessStudentRecord(id);
        var transcript = await _studentService.GetTranscriptAsync(id, cancellationToken);
        return Ok(transcript);
    }

    private void EnsureCanAccessStudentRecord(int targetStudentId)
    {
        if (User.IsInRole("Teacher"))
        {
            return;
        }

        var studentIdClaim = User.FindFirstValue("student_id");
        if (!int.TryParse(studentIdClaim, out var authenticatedStudentId) || authenticatedStudentId != targetStudentId)
        {
            throw new ForbiddenException(
                "Forbidden",
                "A student user may access only his or her own student record and transcript.");
        }
    }
}

