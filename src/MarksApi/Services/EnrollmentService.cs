using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Enrollments;
using MarksApi.Exceptions;
using MarksApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(AppDbContext dbContext, ILogger<EnrollmentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<EnrollmentResponse> EnrollStudentAsync(
        int courseId,
        CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var studentId = request.StudentId!.Value;

        var course = await _dbContext.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new { c.Id, c.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            throw new NotFoundException("Course not found", $"Course with ID {courseId} was not found.");
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.RegistrationNo, s.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("Student not found", $"Student with ID {studentId} was not found.");
        }

        var entity = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            Mark = null,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _dbContext.Enrollments.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate enrollment rejected for StudentId {StudentId} in CourseId {CourseId}",
                studentId, courseId);
            throw new ConflictException(
                "Duplicate enrollment",
                "This student is already enrolled in the specified course.");
        }

        _logger.LogInformation("Enrolled student {StudentId} into course {CourseId} (EnrollmentId: {EnrollmentId})",
            studentId, courseId, entity.Id);

        return new EnrollmentResponse
        {
            Id = entity.Id,
            StudentId = student.Id,
            StudentRegistrationNo = student.RegistrationNo,
            StudentFullName = student.FullName,
            CourseId = course.Id,
            CourseCode = course.Code,
            Mark = entity.Mark,
            EnrolledAtUtc = entity.EnrolledAtUtc,
            Version = entity.Version
        };
    }

    public async Task<PagedResponse<EnrollmentResponse>> GetCourseEnrollmentsAsync(
        int courseId,
        int page,
        int pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var courseExists = await _dbContext.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);

        if (!courseExists)
        {
            throw new NotFoundException("Course not found", $"Course with ID {courseId} was not found.");
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        IQueryable<Enrollment> query = _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Student.FullName.ToLower().Contains(term) ||
                e.Student.RegistrationNo.ToLower().Contains(term));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "mark" or "mark_asc" => query.OrderBy(e => e.Mark).ThenBy(e => e.Id),
            "mark_desc" => query.OrderByDescending(e => e.Mark).ThenBy(e => e.Id),
            "student" or "name" or "name_asc" => query.OrderBy(e => e.Student.FullName).ThenBy(e => e.Id),
            "student_desc" or "name_desc" => query.OrderByDescending(e => e.Student.FullName).ThenBy(e => e.Id),
            "id_desc" => query.OrderByDescending(e => e.Id),
            _ => query.OrderBy(e => e.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(e => new EnrollmentResponse
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentRegistrationNo = e.Student.RegistrationNo,
                StudentFullName = e.Student.FullName,
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc,
                Version = e.Version
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<EnrollmentResponse>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<MarkResponse> UpdateMarkAsync(
        int enrollmentId,
        UpdateMarkRequest request,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            throw new NotFoundException("Enrollment not found", $"Enrollment with ID {enrollmentId} was not found.");
        }

        // If the caller sent a row version, attach it as the expected OriginalValue so EF checks xmin on UPDATE
        if (request.Version.HasValue)
        {
            _dbContext.Entry(enrollment).Property(e => e.Version).OriginalValue = request.Version.Value;
        }

        var oldMark = enrollment.Mark;
        enrollment.Mark = request.Mark!.Value;

        if (_dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            // SQLite in unit tests does not maintain PostgreSQL's xmin column automatically
            if (request.Version.HasValue && request.Version.Value != enrollment.Version)
            {
                throw new ConflictException(
                    "Concurrency conflict",
                    "The mark for this enrollment was modified by another user. Please refresh and try again.");
            }
            enrollment.Version++;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Optimistic concurrency conflict detected while updating mark for EnrollmentId {EnrollmentId}",
                enrollmentId);
            throw new ConflictException(
                "Concurrency conflict",
                "The mark for this enrollment was modified by another user. Please refresh and try again.");
        }

        _logger.LogInformation("Updated mark for EnrollmentId {EnrollmentId} (StudentId: {StudentId}, CourseId: {CourseId}) from {OldMark} to {NewMark}",
            enrollment.Id, enrollment.StudentId, enrollment.CourseId, oldMark, enrollment.Mark);

        return new MarkResponse
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            Mark = enrollment.Mark,
            Version = enrollment.Version
        };
    }

    public async Task DeleteEnrollmentAsync(int enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            throw new NotFoundException("Enrollment not found", $"Enrollment with ID {enrollmentId} was not found.");
        }

        _dbContext.Enrollments.Remove(enrollment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted enrollment {EnrollmentId} (StudentId: {StudentId}, CourseId: {CourseId})",
            enrollment.Id, enrollment.StudentId, enrollment.CourseId);
    }
}

