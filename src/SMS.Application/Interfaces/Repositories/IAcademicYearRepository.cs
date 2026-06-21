using SMS.Application.DTOs;
using SMS.Domain.Entities.Shared;

namespace SMS.Application.Interfaces.Repositories;

public interface IAcademicYearRepository
{
    Task<AcademicYear> GetCurrentAsync(bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicYear>> GetAllAsync(int schoolId, CancellationToken cancellationToken = default);
    Task<AcademicYear?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<bool> CanDeleteAsync(int yearId, CancellationToken cancellationToken = default);
    void Add(AcademicYear academicYear);
    void Remove(AcademicYear academicYear);
}
