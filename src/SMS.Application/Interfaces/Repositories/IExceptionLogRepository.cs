using SMS.Domain.Entities.Shared;

namespace SMS.Application.Interfaces.Repositories;

public interface IExceptionLogRepository
{
    Task<int> AddAndSaveAsync(AppExceptionLog log, CancellationToken cancellationToken = default);

    Task<AppExceptionLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AppExceptionLog> Items, int TotalCount)> GetPagedAsync(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
