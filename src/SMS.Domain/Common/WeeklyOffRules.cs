namespace SMS.Domain.Common;

[Flags]
public enum WeeklyOffDays
{
    None = 0,
    Sunday = 1,
    Monday = 2,
    Tuesday = 4,
    Wednesday = 8,
    Thursday = 16,
    Friday = 32,
    Saturday = 64
}

public static class WeeklyOffRules
{
    public static bool IsOffDay(DayOfWeek day, WeeklyOffDays offDays) =>
        offDays.HasFlag(ToFlag(day));

    public static string GetOffDayTitle(DayOfWeek day) => $"Weekly Off ({day})";

    public static WeeklyOffDays ToFlag(DayOfWeek day) =>
        day switch
        {
            DayOfWeek.Sunday => WeeklyOffDays.Sunday,
            DayOfWeek.Monday => WeeklyOffDays.Monday,
            DayOfWeek.Tuesday => WeeklyOffDays.Tuesday,
            DayOfWeek.Wednesday => WeeklyOffDays.Wednesday,
            DayOfWeek.Thursday => WeeklyOffDays.Thursday,
            DayOfWeek.Friday => WeeklyOffDays.Friday,
            DayOfWeek.Saturday => WeeklyOffDays.Saturday,
            _ => WeeklyOffDays.None
        };

    public static IReadOnlyList<DayOfWeek> GetSelectedDays(WeeklyOffDays offDays)
    {
        var days = new List<DayOfWeek>();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (IsOffDay(day, offDays))
            {
                days.Add(day);
            }
        }

        return days;
    }
}
