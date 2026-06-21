using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SMS.Application.Common;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;

namespace SMS.Infrastructure.Services;

public class ExceptionLogService(
    IExceptionLogRepository exceptionLogRepository,
    IHttpContextAccessor httpContextAccessor) : IExceptionLogService
{
    public async Task<int> LogAsync(
        Exception exception,
        string source,
        string? contextJson = null,
        CancellationToken cancellationToken = default)
    {
        var (sqlError, constraint, innerMessage) = DbExceptionHelper.ExtractSqlDetails(exception);
        var user = httpContextAccessor.HttpContext?.User;

        var log = new AppExceptionLog
        {
            Source = Truncate(source, 200),
            Message = Truncate(exception.Message, 2000) ?? string.Empty,
            ExceptionType = Truncate(exception.GetType().FullName ?? exception.GetType().Name, 300) ?? string.Empty,
            InnerMessage = Truncate(innerMessage, 2000),
            StackTrace = Truncate(exception.StackTrace, 8000),
            SqlErrorNumber = sqlError,
            ConstraintName = Truncate(constraint, 200),
            UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserEmail = user?.FindFirstValue(ClaimTypes.Email) ?? user?.Identity?.Name,
            ContextJson = Truncate(contextJson, 4000)
        };

        return await exceptionLogRepository.AddAndSaveAsync(log, cancellationToken);
    }

    public async Task<Exception> LogAndWrapAsync(
        Exception exception,
        string source,
        string? contextJson = null,
        CancellationToken cancellationToken = default)
    {
        int logId;
        try
        {
            logId = await LogAsync(exception, source, contextJson, cancellationToken);
        }
        catch
        {
            return new InvalidOperationException(DbExceptionHelper.GetUserMessage(exception));
        }

        var friendly = DbExceptionHelper.GetUserMessage(exception);
        return new InvalidOperationException($"{friendly} (Error log #{logId})");
    }

    public async Task<PagedResultDto<ExceptionLogListItemDto>> GetLogsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var (items, totalCount) = await exceptionLogRepository.GetPagedAsync(search, skip, pageSize, cancellationToken);
        var dtos = items.Select(x => new ExceptionLogListItemDto(
            x.Id,
            x.CreatedAt,
            x.Source,
            x.Message,
            x.ExceptionType,
            x.UserEmail,
            x.SqlErrorNumber,
            x.ConstraintName)).ToList();

        return new PagedResultDto<ExceptionLogListItemDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ExceptionLogDetailDto?> GetLogAsync(int id, CancellationToken cancellationToken = default)
    {
        var log = await exceptionLogRepository.GetByIdAsync(id, cancellationToken);
        if (log is null)
        {
            return null;
        }

        var fullReport = DbExceptionHelper.BuildFullReport(
            log.Id,
            log.CreatedAt,
            log.Source,
            log.Message,
            log.ExceptionType,
            log.InnerMessage,
            log.StackTrace,
            log.SqlErrorNumber,
            log.ConstraintName,
            log.UserId,
            log.UserEmail,
            log.ContextJson);

        return new ExceptionLogDetailDto(
            log.Id,
            log.CreatedAt,
            log.Source,
            log.Message,
            log.ExceptionType,
            log.InnerMessage,
            log.StackTrace,
            log.SqlErrorNumber,
            log.ConstraintName,
            log.UserId,
            log.UserEmail,
            log.ContextJson,
            fullReport);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
