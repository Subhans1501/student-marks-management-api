using MarksApi.Dtos.Common;
using MarksApi.Dtos.Enrollments;

namespace MarksApi.Services.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponse> EnrollStudentAsync(
        int courseId,
        CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<EnrollmentResponse>> GetCourseEnrollmentsAsync(
        int courseId,
        int page,
        int pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default);

    Task<MarkResponse> UpdateMarkAsync(
        int enrollmentId,
        UpdateMarkRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteEnrollmentAsync(int enrollmentId, CancellationToken cancellationToken = default);
}

