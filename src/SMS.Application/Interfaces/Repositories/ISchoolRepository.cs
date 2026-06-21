using SMS.Domain.Entities.Shared;

namespace SMS.Application.Interfaces.Repositories;

public interface ISchoolRepository
{
    Task<School?> GetFirstAsync(bool tracking = false, CancellationToken cancellationToken = default);
    Task<School?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
}

