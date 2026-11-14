using MarksApi.Contracts;

namespace MarksApi.Services;

public interface IMarksService
{
    Task<bool> CheckDatabaseHealthAsync(CancellationToken cancellationToken);
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<StudentResponse>> ListStudentsAsync(int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken);
    Task<StudentResponse?> GetStudentAsync(int id, CancellationToken cancellationToken);
    Task<StudentResponse> CreateStudentAsync(CreateStudentRequest request, CancellationToken cancellationToken);
    Task<StudentResponse?> UpdateStudentAsync(int id, UpdateStudentRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteStudentAsync(int id, CancellationToken cancellationToken);
    Task<TranscriptResponse?> GetTranscriptAsync(int id, CancellationToken cancellationToken);
    Task<PagedResponse<CourseResponse>> ListCoursesAsync(int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken);
    Task<CourseResponse?> GetCourseAsync(int id, CancellationToken cancellationToken);
    Task<CourseResponse> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken);
    Task<CourseResponse?> UpdateCourseAsync(int id, UpdateCourseRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteCourseAsync(int id, CancellationToken cancellationToken);
    Task<EnrollmentResponse> CreateEnrollmentAsync(int courseId, CreateEnrollmentRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<EnrollmentResponse>?> ListCourseEnrollmentsAsync(int courseId, int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken);
    Task<EnrollmentResponse?> GetEnrollmentAsync(int id, CancellationToken cancellationToken);
    Task<EnrollmentResponse?> UpdateMarkAsync(int id, UpdateMarkRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteEnrollmentAsync(int id, CancellationToken cancellationToken);
    Task<CourseStatsResponse?> GetCourseStatsAsync(int courseId, CancellationToken cancellationToken);
}
