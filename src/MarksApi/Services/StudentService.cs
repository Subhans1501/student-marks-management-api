using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Reports;
using MarksApi.Dtos.Students;
using MarksApi.Exceptions;
using MarksApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class StudentService : IStudentService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StudentService> _logger;

    public StudentService(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger<StudentService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PagedResponse<StudentResponse>> GetStudentsAsync(
        int page,
        int pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        IQueryable<Student> query = _dbContext.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.FullName.ToLower().Contains(term) ||
                s.RegistrationNo.ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "name" or "fullname" or "name_asc" => query.OrderBy(s => s.FullName).ThenBy(s => s.Id),
            "name_desc" or "fullname_desc" => query.OrderByDescending(s => s.FullName).ThenBy(s => s.Id),
            "reg" or "registrationno" or "reg_asc" => query.OrderBy(s => s.RegistrationNo).ThenBy(s => s.Id),
            "reg_desc" or "registrationno_desc" => query.OrderByDescending(s => s.RegistrationNo).ThenBy(s => s.Id),
            "created_desc" => query.OrderByDescending(s => s.CreatedAtUtc).ThenBy(s => s.Id),
            "id_desc" => query.OrderByDescending(s => s.Id),
            _ => query.OrderBy(s => s.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<StudentResponse>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<StudentResponse> GetStudentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("Student not found", $"Student with ID {id} was not found.");
        }

        return student;
    }

    public async Task<StudentResponse> CreateStudentAsync(CreateStudentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Student
        {
            RegistrationNo = request.RegistrationNo.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Students.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate student creation rejected for RegistrationNo '{RegistrationNo}' or Email '{Email}'",
                entity.RegistrationNo, entity.Email);
            throw new ConflictException(
                "Duplicate student",
                "A student with this registration number or email already exists.");
        }

        _logger.LogInformation("Created student {StudentId} with RegistrationNo '{RegistrationNo}'",
            entity.Id, entity.RegistrationNo);

        return new StudentResponse
        {
            Id = entity.Id,
            RegistrationNo = entity.RegistrationNo,
            FullName = entity.FullName,
            Email = entity.Email,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task<StudentResponse> UpdateStudentAsync(int id, UpdateStudentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("Student not found", $"Student with ID {id} was not found.");
        }

        entity.RegistrationNo = request.RegistrationNo.Trim();
        entity.FullName = request.FullName.Trim();
        entity.Email = request.Email.Trim().ToLowerInvariant();

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate student update rejected for StudentId {StudentId}", id);
            throw new ConflictException(
                "Duplicate student",
                "A student with this registration number or email already exists.");
        }

        _logger.LogInformation("Updated student {StudentId} ('{RegistrationNo}')", entity.Id, entity.RegistrationNo);

        return new StudentResponse
        {
            Id = entity.Id,
            RegistrationNo = entity.RegistrationNo,
            FullName = entity.FullName,
            Email = entity.Email,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task DeleteStudentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("Student not found", $"Student with ID {id} was not found.");
        }

        var hasEnrollments = await _dbContext.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.StudentId == id, cancellationToken);

        if (hasEnrollments)
        {
            _logger.LogWarning("Attempt to delete student {StudentId} blocked because enrollments exist.", id);
            throw new ConflictException(
                "Cannot delete student",
                "A student that still has enrollments cannot be deleted.");
        }

        _dbContext.Students.Remove(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsForeignKeyRestrictViolation(ex))
        {
            _logger.LogWarning("Foreign key restrict blocked deletion of student {StudentId}", id);
            throw new ConflictException(
                "Cannot delete student",
                "A student that still has enrollments or linked accounts cannot be deleted.");
        }

        _logger.LogInformation("Deleted student {StudentId} ('{RegistrationNo}')", entity.Id, entity.RegistrationNo);
    }

    public async Task<TranscriptResponse> GetTranscriptAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var passMark = _configuration.GetValue<int?>("DepartmentRules:PassMark") ?? 50;

        var student = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                s.Id,
                s.RegistrationNo,
                s.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("Student not found", $"Student with ID {studentId} was not found.");
        }

        var courses = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderBy(e => e.Course.Code)
            .Select(e => new TranscriptCourseItem
            {
                Code = e.Course.Code,
                Title = e.Course.Title,
                CreditHours = e.Course.CreditHours,
                Mark = e.Mark,
                Passed = e.Mark.HasValue && e.Mark.Value >= passMark
            })
            .ToListAsync(cancellationToken);

        var gradedCourses = courses.Where(c => c.Mark.HasValue).ToList();
        double? weightedAverage = null;

        if (gradedCourses.Count > 0)
        {
            var totalWeightedPoints = gradedCourses.Sum(c => c.Mark!.Value * c.CreditHours);
            var totalCredits = gradedCourses.Sum(c => c.CreditHours);
            if (totalCredits > 0)
            {
                weightedAverage = Math.Round((double)totalWeightedPoints / totalCredits, 2);
            }
        }

        return new TranscriptResponse
        {
            StudentId = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Courses = courses,
            WeightedAverage = weightedAverage
        };
    }
}

