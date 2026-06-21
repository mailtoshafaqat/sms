using System.Text.RegularExpressions;

namespace SMS.Application.Common;

public static partial class DbExceptionHelper
{
    public static (int? SqlErrorNumber, string? ConstraintName, string? InnerMessage) ExtractSqlDetails(Exception exception)
    {
        Exception? current = exception;
        string? innerMessage = null;
        int? sqlNumber = null;

        while (current is not null)
        {
            innerMessage = current.Message;
            sqlNumber ??= ExtractSqlErrorNumber(current.Message);

            var constraint = ExtractConstraintName(current.Message);
            if (!string.IsNullOrWhiteSpace(constraint))
            {
                return (sqlNumber, constraint, innerMessage);
            }

            current = current.InnerException;
        }

        return (sqlNumber, ExtractConstraintName(exception.Message), innerMessage);
    }

    public static string GetUserMessage(Exception exception)
    {
        var (_, constraint, _) = ExtractSqlDetails(exception);
        if (!string.IsNullOrWhiteSpace(constraint))
        {
            return TranslateConstraint(constraint);
        }

        if (exception.GetType().Name is "DbUpdateException")
        {
            return "Could not save changes because a database rule was violated. Check for duplicate student code, roll number, or gate device ID.";
        }

        return exception.Message;
    }

    public static string BuildFullReport(
        int id,
        DateTime createdAt,
        string source,
        string message,
        string exceptionType,
        string? innerMessage,
        string? stackTrace,
        int? sqlErrorNumber,
        string? constraintName,
        string? userId,
        string? userEmail,
        string? contextJson)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "SMS Error Report",
            "================",
            $"Log ID: {id}",
            $"Time (UTC): {createdAt:yyyy-MM-dd HH:mm:ss}",
            $"Source: {source}",
            $"User: {userEmail ?? userId ?? "(unknown)"}",
            $"Type: {exceptionType}",
            $"Message: {message}",
            string.IsNullOrWhiteSpace(innerMessage) ? null : $"Inner: {innerMessage}",
            sqlErrorNumber is null ? null : $"SQL Error: {sqlErrorNumber}",
            string.IsNullOrWhiteSpace(constraintName) ? null : $"Constraint: {constraintName}",
            string.IsNullOrWhiteSpace(contextJson) ? null : $"Context: {contextJson}",
            string.IsNullOrWhiteSpace(stackTrace) ? null : "Stack trace:",
            stackTrace
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string TranslateConstraint(string constraint)
    {
        var key = constraint.ToUpperInvariant();

        if (key.Contains("STUDENTS_SCHOOLID_STUDENTCODE", StringComparison.Ordinal))
        {
            return "A student with this code already exists. Each student must have a unique student code (leave blank to auto-generate).";
        }

        if (key.Contains("ACADEMICYEARID_SECTIONID_ROLLNUMBER", StringComparison.Ordinal)
            || (key.Contains("STUDENTENROLLMENTS", StringComparison.Ordinal) && key.Contains("ROLL", StringComparison.Ordinal)))
        {
            return "This roll number is already used in the selected class section for the current academic year.";
        }

        if (key.Contains("BIOMETRICDEVICEID_BIOMETRICUSERID", StringComparison.Ordinal))
        {
            return "This gate device user ID is already assigned to another student. Use a unique fingerprint/face ID.";
        }

        if (key.Contains("STUDENTID_ACADEMICYEARID", StringComparison.Ordinal))
        {
            return "This student already has an enrollment for the current academic year.";
        }

        return $"A database constraint was violated ({constraint}).";
    }

    private static int? ExtractSqlErrorNumber(string message)
    {
        var match = SqlErrorRegex().Match(message);
        return match.Success && int.TryParse(match.Groups[1].Value, out var number) ? number : null;
    }

    private static string? ExtractConstraintName(string message)
    {
        var match = ConstraintRegex().Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("constraint [\"']?([^\"'.]+)[\"']?", RegexOptions.IgnoreCase)]
    private static partial Regex ConstraintRegex();

    [GeneratedRegex(@"error:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlErrorRegex();
}
