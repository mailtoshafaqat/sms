using SMS.Domain.Enums;

namespace SMS.Application.Common;

public static class NotificationMessageBuilder
{
    public const string DefaultAbsentTemplate =
        "{SchoolName}: {StudentName} was marked ABSENT on {Date} ({ClassSection}). Please contact the school if this is incorrect.";

    public const string DefaultLateTemplate =
        "{SchoolName}: {StudentName} arrived LATE today ({Date}) at {Time}.";

    public const string DefaultCheckInTemplate =
        "{SchoolName}: {StudentName} checked IN at {Time} on {Date} ({ClassSection}).";

    public const string DefaultCheckOutTemplate =
        "{SchoolName}: {StudentName} checked OUT at {Time} on {Date}.";

    public const string DefaultLeaveTemplate =
        "{SchoolName}: {StudentName} is on LEAVE on {Date} ({ClassSection}).";

    public const string DefaultPresentTemplate =
        "{SchoolName}: {StudentName} was marked PRESENT on {Date} ({ClassSection}).";

    public static string BuildAbsentMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        string className,
        string sectionName) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultAbsentTemplate : template,
            BuildValues(schoolName, studentName, date, className, sectionName));

    public static string BuildLateMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        TimeOnly? time = null) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultLateTemplate : template,
            BuildValues(schoolName, studentName, date, time: time));

    public static string BuildCheckInMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        string className,
        string sectionName,
        TimeOnly time) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultCheckInTemplate : template,
            BuildValues(schoolName, studentName, date, className, sectionName, time));

    public static string BuildCheckOutMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        TimeOnly time) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultCheckOutTemplate : template,
            BuildValues(schoolName, studentName, date, time: time));

    public static string BuildLeaveMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        string className,
        string sectionName) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultLeaveTemplate : template,
            BuildValues(schoolName, studentName, date, className, sectionName));

    public static string BuildPresentMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        string className,
        string sectionName) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultPresentTemplate : template,
            BuildValues(schoolName, studentName, date, className, sectionName));

    public static AttendanceNotificationType? ToNotificationType(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Absent => AttendanceNotificationType.Absent,
        AttendanceStatus.Late => AttendanceNotificationType.Late,
        AttendanceStatus.Leave => AttendanceNotificationType.Leave,
        AttendanceStatus.Present => AttendanceNotificationType.Present,
        _ => null
    };

    private static Dictionary<string, string> BuildValues(
        string schoolName,
        string studentName,
        DateOnly date,
        string? className = null,
        string? sectionName = null,
        TimeOnly? time = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SchoolName"] = schoolName,
            ["StudentName"] = studentName,
            ["Date"] = date.ToString("dd MMM yyyy"),
            ["Time"] = time?.ToString("HH:mm") ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(className))
        {
            values["Class"] = className;
        }

        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            values["Section"] = sectionName;
        }

        if (!string.IsNullOrWhiteSpace(className) && !string.IsNullOrWhiteSpace(sectionName))
        {
            values["ClassSection"] = $"{className}-{sectionName}";
        }

        return values;
    }

    private static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
