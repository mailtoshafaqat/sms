namespace SMS.Application.DTOs;

public sealed class AcademicYearSessionAlertDto
{
    public int AcademicYearId { get; init; }
    public string YearName { get; init; } = string.Empty;
    public DateOnly EndDate { get; init; }
    public int DaysRemaining { get; init; }
    public bool IsPastEndDate { get; init; }
    public string Severity { get; init; } = "warning";
    public string Message { get; init; } = string.Empty;
}
