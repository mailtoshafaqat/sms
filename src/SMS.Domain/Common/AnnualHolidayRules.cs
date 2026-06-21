namespace SMS.Domain.Common;

public static class AnnualHolidayRules
{
    public static bool MatchesDate(bool repeatsAnnually, int recurringMonth, int recurringDay, DateOnly date) =>
        repeatsAnnually
        && recurringMonth == date.Month
        && recurringDay == date.Day;

    public static string GetDisplayLabel(int month, int day, string title) =>
        $"{title} ({day:00}/{month:00} every year)";
}
