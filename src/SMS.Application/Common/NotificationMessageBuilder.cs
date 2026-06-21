namespace SMS.Application.Common;

public static class NotificationMessageBuilder
{
    public const string DefaultAbsentTemplate =
        "{SchoolName}: {StudentName} was marked ABSENT on {Date} ({ClassSection}). Please contact the school if this is incorrect.";

    public const string DefaultLateTemplate =
        "{SchoolName}: {StudentName} arrived LATE today ({Date}).";

    public static string BuildAbsentMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date,
        string className,
        string sectionName) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultAbsentTemplate : template,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SchoolName"] = schoolName,
                ["StudentName"] = studentName,
                ["Date"] = date.ToString("dd MMM yyyy"),
                ["Class"] = className,
                ["Section"] = sectionName,
                ["ClassSection"] = $"{className}-{sectionName}"
            });

    public static string BuildLateMessage(
        string? template,
        string schoolName,
        string studentName,
        DateOnly date) =>
        ApplyTemplate(
            string.IsNullOrWhiteSpace(template) ? DefaultLateTemplate : template,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SchoolName"] = schoolName,
                ["StudentName"] = studentName,
                ["Date"] = date.ToString("dd MMM yyyy")
            });

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
