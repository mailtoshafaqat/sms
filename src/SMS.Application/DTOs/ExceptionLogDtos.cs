namespace SMS.Application.DTOs;

public record ExceptionLogListItemDto(
    int Id,
    DateTime CreatedAt,
    string Source,
    string Message,
    string ExceptionType,
    string? UserEmail,
    int? SqlErrorNumber,
    string? ConstraintName);

public record ExceptionLogDetailDto(
    int Id,
    DateTime CreatedAt,
    string Source,
    string Message,
    string ExceptionType,
    string? InnerMessage,
    string? StackTrace,
    int? SqlErrorNumber,
    string? ConstraintName,
    string? UserId,
    string? UserEmail,
    string? ContextJson,
    string FullReport);
