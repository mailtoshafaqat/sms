using SMS.Application.DTOs;
using SMS.Domain.Common;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces;

public interface ISchoolService
{
    Task<SchoolSettingsDto?> GetSchoolSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSchoolSettingsAsync(SchoolSettingsDto dto, CancellationToken cancellationToken = default);
    Task<string> UploadLogoAsync(int schoolId, Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task RemoveLogoAsync(int schoolId, CancellationToken cancellationToken = default);
}

public interface IStudentService
{
    Task<PagedResultDto<StudentListItemDto>> GetStudentsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 25,
        string? userId = null,
        StudentListFilter filter = StudentListFilter.ActiveOnly,
        CancellationToken cancellationToken = default);
    Task<StudentFormDto?> GetStudentAsync(int id, CancellationToken cancellationToken = default);
    Task<int> SaveStudentAsync(StudentFormDto dto, CancellationToken cancellationToken = default);
    Task DeleteStudentAsync(int id, CancellationToken cancellationToken = default);
    Task<string> UploadPhotoAsync(int studentId, Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task RemovePhotoAsync(int studentId, CancellationToken cancellationToken = default);
}

public interface IStudentPromotionService
{
    Task<IReadOnlyList<StudentPromotionCandidateDto>> GetCandidatesAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<PromotionResultDto> PromoteStudentsAsync(
        int sectionId,
        IReadOnlyList<int> studentIds,
        int? targetSectionId,
        PromotionSource source,
        string? promotedByUserId,
        string? notes = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentPromotionHistoryDto>> GetRecentHistoryAsync(int take = 20, CancellationToken cancellationToken = default);
}

public interface IClassService
{
    Task<IReadOnlyList<ClassRoomDto>> GetClassesAsync(CancellationToken cancellationToken = default);
    Task<PagedResultDto<ClassRoomDto>> GetClassesPagedAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<int> SaveClassAsync(string name, int? id = null, CancellationToken cancellationToken = default);
    Task<int> SaveSectionAsync(int classRoomId, string sectionName, int? sectionId = null, CancellationToken cancellationToken = default);
    Task SetClassActiveAsync(int classId, bool isActive, CancellationToken cancellationToken = default);
    Task SetSectionActiveAsync(int sectionId, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteClassAsync(int classId, CancellationToken cancellationToken = default);
    Task DeleteSectionAsync(int sectionId, CancellationToken cancellationToken = default);
    Task MoveClassAsync(int classId, bool moveUp, CancellationToken cancellationToken = default);
    Task MoveSectionAsync(int sectionId, bool moveUp, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSectionOptionDto>> GetSectionOptionsAsync(string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default);
    Task<PagedResultDto<SectionDto>> GetSectionsPagedAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<ClassTreeDto> GetClassTreeAsync(CancellationToken cancellationToken = default);
}

public interface IAttendanceService
{
    Task<DailyAttendanceSummaryDto> GetDailySummaryAsync(DateOnly date, string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyAttendanceRowDto>> GetDailyAttendanceRowsAsync(DateOnly date, string? userId = null, CancellationToken cancellationToken = default);
    Task<ManualAttendanceSheetResultDto> GetManualAttendanceSheetAsync(int sectionId, DateOnly date, string? userId = null, CancellationToken cancellationToken = default);
    Task SaveManualAttendanceAsync(int sectionId, DateOnly date, IReadOnlyList<ManualAttendanceRowDto> rows, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceLogDto>> GetRecentLogsAsync(int take = 50, DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<bool> ProcessBiometricScanAsync(string biometricUserId, int deviceId, ScanDirection? direction = null, CancellationToken cancellationToken = default);
    Task FinalizeDailyAttendanceAsync(DateOnly date, string? userId = null, CancellationToken cancellationToken = default);
    Task<MonthlyRegisterDto> GetMonthlyRegisterAsync(int sectionId, int year, int month, string? userId = null, CancellationToken cancellationToken = default);
    Task<AttendanceCalendarMonthDto> GetAttendanceCalendarAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchoolHolidayDto>> GetAnnualHolidaysAsync(CancellationToken cancellationToken = default);
    Task MarkHolidayAsync(DateOnly date, string title, string? description = null, bool repeatsAnnually = false, CancellationToken cancellationToken = default);
    Task RemoveHolidayAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task RemoveAnnualHolidayAsync(int holidayId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendancePatternStudentDto>> GetAttendancePatternAsync(
        DateOnly from,
        DateOnly to,
        AttendanceStatus status,
        int minOccurrences = 3,
        int minConsecutive = 0,
        int? sectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default);
}

public interface IBiometricConfigService
{
    Task<BiometricDeviceDto?> GetActiveDeviceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiometricDeviceDto>> GetEnabledDevicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiometricDeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveDeviceAsync(BiometricDeviceDto dto, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(int deviceId, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardAsync(string? userId = null, CancellationToken cancellationToken = default);
}

public interface IAcademicYearService
{
    Task<IReadOnlyList<AcademicYearDto>> GetYearsAsync(CancellationToken cancellationToken = default);
    Task<int> SaveYearAsync(AcademicYearFormDto dto, CancellationToken cancellationToken = default);
    Task SetCurrentYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task DeleteYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task<AcademicYearSessionAlertDto?> GetSessionEndAlertAsync(CancellationToken cancellationToken = default);
}

public interface ITeacherAssignmentService
{
    Task<IReadOnlyList<TeacherStaffDto>> GetTeachersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionTeacherAssignmentDto>> GetSectionAssignmentsAsync(CancellationToken cancellationToken = default);
    Task<int> EnsureTeacherProfileAsync(string userId, string firstName, string lastName, string? employeeCode = null, CancellationToken cancellationToken = default);
    Task<StaffMemberFormDto?> GetStaffMemberAsync(int teacherId, CancellationToken cancellationToken = default);
    Task<int> SaveStaffMemberAsync(StaffMemberFormDto dto, CancellationToken cancellationToken = default);
    Task AssignSectionTeacherAsync(int sectionId, int? teacherId, CancellationToken cancellationToken = default);
}

public interface IAttendanceNotificationService
{
    Task QueueAbsentNotificationsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task QueueLateNotificationAsync(int studentId, DateOnly date, DateTime? eventTime = null, CancellationToken cancellationToken = default);
    Task QueueCheckInNotificationAsync(int studentId, DateOnly date, DateTime scanTime, CancellationToken cancellationToken = default);
    Task QueueCheckOutNotificationAsync(int studentId, DateOnly date, DateTime scanTime, CancellationToken cancellationToken = default);
    Task QueueAttendanceStatusNotificationAsync(int studentId, DateOnly date, AttendanceStatus status, CancellationToken cancellationToken = default);
    Task QueueAttendanceNotificationAsync(int studentId, DateOnly date, AttendanceNotificationType type, DateTime? eventTime = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceNotificationDto>> GetNotificationsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task MarkSentAsync(int notificationId, CancellationToken cancellationToken = default);
    string BuildWhatsAppUrl(string phone, string message);
}

public interface IStaffAttendanceService
{
    Task<StaffAttendanceSheetResultDto> GetSheetAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task SaveSheetAsync(DateOnly date, IReadOnlyList<StaffAttendanceRowDto> rows, string? userId, CancellationToken cancellationToken = default);
    Task<StaffMonthlyRegisterDto> GetMonthlyRegisterAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<StaffAttendanceSummaryResultDto> GetSummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public interface IBiometricDeviceConnector
{
    Task<bool> TestConnectionAsync(BiometricDeviceDto device, CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}

public interface ILocalBiometricService
{
    Task<string> EnrollFaceAsync(int studentId, IReadOnlyList<float> descriptor, CancellationToken cancellationToken = default);
    Task<LocalBiometricMatchDto?> MatchFaceAsync(
        IReadOnlyList<float> descriptor,
        FaceMatchMode mode = FaceMatchMode.Gate,
        CancellationToken cancellationToken = default);
    Task<string> EnrollFingerprintAsync(int studentId, string credentialId, CancellationToken cancellationToken = default);
    Task<LocalBiometricMatchDto?> MatchFingerprintAsync(string credentialId, CancellationToken cancellationToken = default);
    Task<LocalBiometricScanResultDto> ScanAsync(int studentId, BiometricType type, ScanDirection? direction = null, CancellationToken cancellationToken = default);
    Task<LocalBiometricScanResultDto> ScanByExternalIdAsync(string externalId, BiometricType type, ScanDirection? direction = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GateFaceEnrollmentDto>> GetFaceEnrollmentsAsync(CancellationToken cancellationToken = default);
    Task<int> GetFaceEnrollmentCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetFaceSampleCountAsync(int studentId, CancellationToken cancellationToken = default);
}

