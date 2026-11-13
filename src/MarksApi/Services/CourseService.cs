using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Reports;
using MarksApi.Exceptions;
using MarksApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class CourseService : ICourseService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CourseService> _logger;

    public CourseService(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger<CourseService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PagedResponse<CourseResponse>> GetCoursesAsync(
        int page,
        int pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        IQueryable<Course> query = _dbContext.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Code.ToLower().Contains(term) ||
                c.Title.ToLower().Contains(term));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "code" or "code_asc" => query.OrderBy(c => c.Code).ThenBy(c => c.Id),
            "code_desc" => query.OrderByDescending(c => c.Code).ThenBy(c => c.Id),
            "title" or "title_asc" => query.OrderBy(c => c.Title).ThenBy(c => c.Id),
            "title_desc" => query.OrderByDescending(c => c.Title).ThenBy(c => c.Id),
            "credits" or "credithours" or "credits_asc" => query.OrderBy(c => c.CreditHours).ThenBy(c => c.Id),
            "credits_desc" or "credithours_desc" => query.OrderByDescending(c => c.CreditHours).ThenBy(c => c.Id),
            "id_desc" => query.OrderByDescending(c => c.Id),
            _ => query.OrderBy(c => c.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(c => new CourseResponse
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<CourseResponse>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CourseResponse> GetCourseByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var course = await _dbContext.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponse
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            throw new NotFoundException("Course not found", $"Course with ID {id} was not found.");
        }

        return course;
    }

    public async Task<CourseResponse> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Course
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            CreditHours = request.CreditHours!.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Courses.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate course creation rejected for Code '{Code}'", entity.Code);
            throw new ConflictException(
                "Duplicate course",
                "A course with this course code already exists.");
        }

        _logger.LogInformation("Created course {CourseId} ('{Code}')", entity.Id, entity.Code);

        return new CourseResponse
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            CreditHours = entity.CreditHours,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task<CourseResponse> UpdateCourseAsync(int id, UpdateCourseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("Course not found", $"Course with ID {id} was not found.");
        }

        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Title = request.Title.Trim();
        entity.CreditHours = request.CreditHours!.Value;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate course update rejected for CourseId {CourseId}", id);
            throw new ConflictException(
                "Duplicate course",
                "A course with this course code already exists.");
        }

        _logger.LogInformation("Updated course {CourseId} ('{Code}')", entity.Id, entity.Code);

        return new CourseResponse
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            CreditHours = entity.CreditHours,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task DeleteCourseAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("Course not found", $"Course with ID {id} was not found.");
        }

        var hasEnrollments = await _dbContext.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.CourseId == id, cancellationToken);

        if (hasEnrollments)
        {
            _logger.LogWarning("Attempt to delete course {CourseId} blocked because enrollments exist.", id);
            throw new ConflictException(
                "Cannot delete course",
                "A course that still has enrollments cannot be deleted.");
        }

        _dbContext.Courses.Remove(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsForeignKeyRestrictViolation(ex))
        {
            _logger.LogWarning("Foreign key restrict blocked deletion of course {CourseId}", id);
            throw new ConflictException(
                "Cannot delete course",
                "A course that still has enrollments cannot be deleted.");
        }

        _logger.LogInformation("Deleted course {CourseId} ('{Code}')", entity.Id, entity.Code);
    }

    public async Task<CourseStatsResponse> GetCourseStatsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var passMark = _configuration.GetValue<int?>("DepartmentRules:PassMark") ?? 50;

        var course = await _dbContext.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new
            {
                c.Id,
                c.Code
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            throw new NotFoundException("Course not found", $"Course with ID {courseId} was not found.");
        }

        var marks = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.Mark)
            .ToListAsync(cancellationToken);

        var enrolled = marks.Count;
        var gradedMarks = marks.Where(m => m.HasValue).Select(m => m!.Value).ToList();
        var graded = gradedMarks.Count;

        double? average = graded > 0 ? Math.Round(gradedMarks.Average(), 2) : null;
        int? highest = graded > 0 ? gradedMarks.Max() : null;
        int? lowest = graded > 0 ? gradedMarks.Min() : null;
        var passed = gradedMarks.Count(m => m >= passMark);

        return new CourseStatsResponse
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            Enrolled = enrolled,
            Graded = graded,
            Average = average,
            Highest = highest,
            Lowest = lowest,
            Passed = passed
        };
    }
}

