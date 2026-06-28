namespace SMS.Application.Interfaces;

public interface IUserAccessService
{
    Task<bool> IsAdminAsync(string? userId, CancellationToken cancellationToken = default);
    Task<bool> IsCoordinatorAsync(string? userId, CancellationToken cancellationToken = default);
    Task<bool> IsGateKeeperAsync(string? userId, CancellationToken cancellationToken = default);
    Task<bool> IsGateKeeperOnlyAsync(string? userId, CancellationToken cancellationToken = default);
    Task<bool> HasFullAttendanceAccessAsync(string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetAllowedSectionIdsAsync(string? userId, CancellationToken cancellationToken = default);
}
