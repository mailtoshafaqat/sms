using SMS.Domain.Common;

namespace SMS.Domain.Entities.Shared;

public class AppExceptionLog : BaseEntity
{
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string? InnerMessage { get; set; }
    public string? StackTrace { get; set; }
    public int? SqlErrorNumber { get; set; }
    public string? ConstraintName { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? ContextJson { get; set; }
}
