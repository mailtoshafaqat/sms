using SMS.Application.DTOs;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces.Repositories;

public interface IStudentRepository
{
    Task<IReadOnlyList<StudentEnrollment>> GetActiveEnrollmentsAsync(int academicYearId, string? search, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StudentEnrollment> Items, int TotalCount)> GetActiveEnrollmentsPagedAsync(
        int academicYearId,
        string? search,
        int skip,
        int take,
        IReadOnlyList<int>? sectionIds = null,
        StudentListFilter filter = StudentListFilter.ActiveOnly,
        CancellationToken cancellationToken = default);
    Task<StudentEnrollment?> GetEnrollmentAsync(int studentId, int academicYearId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<string?> GetBiometricUserIdAsync(int studentId, CancellationToken cancellationToken = default);
    Task<string?> GetBiometricUserIdAsync(int studentId, BiometricType type, CancellationToken cancellationToken = default);
    Task<Student?> GetStudentByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentEnrollment>> GetEnrollmentsByStudentIdAsync(int studentId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentEnrollment>> GetEnrollmentsForStudentsAsync(IReadOnlyCollection<int> studentIds, int academicYearId, CancellationToken cancellationToken = default);
    Task<bool> RollNumberExistsInSectionAsync(int academicYearId, int sectionId, string rollNumber, int excludeStudentId, CancellationToken cancellationToken = default);
    Task<bool> StudentCodeExistsAsync(int schoolId, string studentCode, int excludeStudentId, CancellationToken cancellationToken = default);
    Task<bool> BiometricUserIdExistsOnDeviceAsync(int deviceId, string biometricUserId, int excludeStudentId, CancellationToken cancellationToken = default);
    void AddStudent(Student student);
    Task<StudentBiometricMap?> GetBiometricMapAsync(int studentId, int deviceId, bool tracking = false, CancellationToken cancellationToken = default);
    void AddBiometricMap(StudentBiometricMap map);
    void RemoveBiometricMap(StudentBiometricMap map);
}

