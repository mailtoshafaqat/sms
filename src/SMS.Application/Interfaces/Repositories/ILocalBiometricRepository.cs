using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces.Repositories;

public interface ILocalBiometricRepository
{
    Task<StudentLocalTemplate?> GetTemplateAsync(int studentId, BiometricType type, bool tracking = false, CancellationToken cancellationToken = default);
    Task<StudentLocalTemplate?> GetByExternalIdAsync(string externalId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentLocalTemplate>> GetFaceTemplatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentLocalTemplate>> GetTemplatesByTypeAsync(BiometricType type, CancellationToken cancellationToken = default);
    void Add(StudentLocalTemplate template);
    void Remove(StudentLocalTemplate template);
}

