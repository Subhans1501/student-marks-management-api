using MarksApi.Dtos.Common;
using MarksApi.Dtos.Reports;
using MarksApi.Dtos.Students;

namespace MarksApi.Services.Interfaces;

public interface IStudentService
{
    Task<PagedResponse<StudentResponse>> GetStudentsAsync(
        int page,
        int pageSize,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default);

    Task<StudentResponse> GetStudentByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<StudentResponse> CreateStudentAsync(CreateStudentRequest request, CancellationToken cancellationToken = default);

    Task<StudentResponse> UpdateStudentAsync(int id, UpdateStudentRequest request, CancellationToken cancellationToken = default);

    Task DeleteStudentAsync(int id, CancellationToken cancellationToken = default);

    Task<TranscriptResponse> GetTranscriptAsync(int studentId, CancellationToken cancellationToken = default);
}

