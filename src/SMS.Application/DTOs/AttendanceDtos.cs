using SMS.Domain.Common;
using SMS.Domain.Enums;

namespace SMS.Application.DTOs;

public class SchoolSettingsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }
    public string? LogoPath { get; set; }
    public TimeOnly SchoolStartTime { get; set; }
    public int LateAfterMinutes { get; set; }
    public TimeOnly SchoolEndTime { get; set; }
    public WeeklyOffDays WeeklyOffDays { get; set; } = WeeklyOffDays.Sunday;
    public bool NotifyAbsent { get; set; } = true;
    public bool NotifyLate { get; set; } = true;
    public string? AbsentNotificationTemplate { get; set; }
    public string? LateNotificationTemplate { get; set; }
}

public record StudentListItemDto(
    int Id,
    string StudentCode,
    string FullName,
    string RollNumber,
    string ClassName,
    string SectionName,
    string? Phone,
    bool IsActive,
    string? PhotoPath);

public record StudentFormDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? PhotoPath { get; set; }
    public int SectionId { get; set; }
    public string RollNumber { get; set; } = string.Empty;
    public string? FingerprintUserId { get; set; }
    public string? FaceUserId { get; set; }
    public string? BiometricUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public record ClassSectionOptionDto(int SectionId, string DisplayName);

public record ClassRoomDto(
    int Id,
    string Name,
    int SectionCount,
    int StudentCount,
    bool IsActive,
    bool CanMoveUp,
    bool CanMoveDown);

public record SectionDto(
    int Id,
    int ClassRoomId,
    string ClassName,
    string SectionName,
    string? ClassTeacherName,
    int StudentCount,
    bool IsActive,
    bool CanMoveUp,
    bool CanMoveDown);

public record ClassTreeDto(
    IReadOnlyList<ClassRoomDto> Classes,
    IReadOnlyList<SectionDto> Sections);

public record ManualAttendanceRowDto
{
    public int StudentId { get; set; }
    public string RollNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public record DailyAttendanceSummaryDto(
    DateOnly Date,
    int EnrolledStudents,
    int MarkedStudents,
    int Present,
    int Absent,
    int Late,
    int Leave,
    int Holidays,
    int Unmarked,
    bool IsHoliday,
    string? HolidayTitle);

public record DailyAttendanceRowDto(
    int StudentId,
    string RollNumber,
    string StudentName,
    string ClassName,
    AttendanceStatus? Status,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    string? Remarks);

public record ChronicLateStudentDto(
    int StudentId,
    string RollNumber,
    string StudentName,
    string ClassSection,
    int LateCount,
    int AttendedDays,
    int ConsecutiveLateDays,
    int CurrentLateStreak,
    DateOnly LastLateDate);

public record ManualAttendanceSheetResultDto(
    IReadOnlyList<ManualAttendanceRowDto> Rows,
    bool CanEdit,
    string? BlockReason);

public record MonthlyRegisterCellDto(
    DateOnly Date,
    AttendanceStatus? Status,
    bool IsNonWorkingDay);

public record MonthlyRegisterStudentRowDto(
    string RollNumber,
    string StudentName,
    IReadOnlyList<MonthlyRegisterCellDto> Days);

public record MonthlyRegisterDto(
    int Year,
    int Month,
    int SectionId,
    string SectionName,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<MonthlyRegisterStudentRowDto> Students);

public record SchoolHolidayDto(
    int Id,
    DateOnly Date,
    string Title,
    string? Description,
    bool RepeatsAnnually,
    int RecurringMonth,
    int RecurringDay);

public record AttendanceCalendarDayDto(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool IsHoliday,
    string? HolidayTitle,
    bool IsWeeklyOff,
    bool IsManualHoliday,
    bool IsAnnualRecurringHoliday);

public record AttendanceCalendarMonthDto(
    int Year,
    int Month,
    IReadOnlyList<AttendanceCalendarDayDto> Days);

public record AttendanceLogDto(
    int Id,
    string StudentName,
    string RollNumber,
    string ClassName,
    DateTime ScanTime,
    ScanDirection Direction,
    BiometricType ScanType,
    int? DeviceId,
    string? DeviceName);

public record BiometricDeviceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public BiometricType BiometricType { get; set; } = BiometricType.Fingerprint;
    public BiometricConnectionType ConnectionType { get; set; } = BiometricConnectionType.Usb;
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 4370;
    public int MachineNumber { get; set; } = 1;
    public string? ComPort { get; set; }
    public int DuplicateScanBlockSeconds { get; set; } = 30;
    public bool IsConnected { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastScanAt { get; set; }
}

public record DashboardStatsDto(
    DailyAttendanceSummaryDto TodaySummary,
    IReadOnlyList<AttendanceLogDto> RecentScans,
    IReadOnlyList<BiometricDeviceDto> EnabledDevices);

public record LocalBiometricMatchDto(
    int StudentId,
    string StudentName,
    string ExternalId,
    float Distance);

public record LocalBiometricScanResultDto(
    bool Success,
    string Message,
    LocalBiometricMatchDto? Match = null);

public record AcademicYearDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent,
    int EnrollmentCount,
    bool CanDelete);

public record AcademicYearFormDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public record TeacherStaffDto(
    int Id,
    string EmployeeCode,
    string FullName,
    string? UserId,
    string? UserEmail,
    bool IsActive,
    IReadOnlyList<string> AssignedSections);

public record SectionTeacherAssignmentDto(
    int SectionId,
    string SectionName,
    string ClassName,
    int? TeacherId,
    string? TeacherName);

public record AttendanceNotificationDto(
    int Id,
    int StudentId,
    string StudentName,
    string RollNumber,
    string ClassName,
    DateOnly AttendanceDate,
    string NotificationType,
    string RecipientPhone,
    string Message,
    bool IsSent,
    DateTime? SentAt,
    string WhatsAppUrl);

