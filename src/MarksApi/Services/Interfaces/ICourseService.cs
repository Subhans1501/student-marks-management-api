using MarksApi.Dtos.Common;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Reports;

namespace MarksApi.Services.Interfaces;

public interface ICourseService
{
    Task<PagedResponse<CourseResponse>> GetCoursesAsync(
        int page,
        int pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default);

    Task<CourseResponse> GetCourseByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CourseResponse> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken = default);

    Task<CourseResponse> UpdateCourseAsync(int id, UpdateCourseRequest request, CancellationToken cancellationToken = default);

    Task DeleteCourseAsync(int id, CancellationToken cancellationToken = default);

    Task<CourseStatsResponse> GetCourseStatsAsync(int courseId, CancellationToken cancellationToken = default);
}

