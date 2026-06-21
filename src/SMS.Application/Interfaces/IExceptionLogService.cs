using SMS.Application.DTOs;

namespace SMS.Application.Interfaces;

public interface IExceptionLogService
{
    Task<int> LogAsync(
        Exception exception,
        string source,
        string? contextJson = null,
        CancellationToken cancellationToken = default);

    Task<Exception> LogAndWrapAsync(
        Exception exception,
        string source,
        string? contextJson = null,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<ExceptionLogListItemDto>> GetLogsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task<ExceptionLogDetailDto?> GetLogAsync(int id, CancellationToken cancellationToken = default);
}
