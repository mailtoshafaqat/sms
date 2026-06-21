using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces.Repositories;

public interface IAttendanceRepository
{
    Task<IReadOnlyList<DailyAttendance>> GetDailyRecordsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentEnrollment>> GetSectionStudentsAsync(int sectionId, int academicYearId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, DailyAttendance>> GetDailyRecordsBySectionAsync(int sectionId, DateOnly date, CancellationToken cancellationToken = default);
    Task<DailyAttendance?> GetDailyRecordAsync(int studentId, DateOnly date, bool tracking = false, CancellationToken cancellationToken = default);
    void AddDailyRecord(DailyAttendance record);
    Task<IReadOnlyList<AttendanceLog>> GetRecentLogsAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceLog>> GetRecentLogsForDateAsync(DateOnly date, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyAttendance>> GetDailyRecordsForSectionAsync(int sectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<StudentBiometricMap?> GetBiometricMapByDeviceAsync(int deviceId, string biometricUserId, CancellationToken cancellationToken = default);
    Task<BiometricDevice?> GetDeviceByIdAsync(int deviceId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<AttendanceLog?> GetLatestLogAsync(int studentId, int deviceId, CancellationToken cancellationToken = default);
    void AddAttendanceLog(AttendanceLog log);
    Task<IReadOnlyList<StudentEnrollment>> GetActiveStudentsAsync(int academicYearId, CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetStudentIdsWithDailyRecordAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchoolHoliday>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchoolHoliday>> GetRecurringHolidaysAsync(CancellationToken cancellationToken = default);
    Task<SchoolHoliday?> GetSpecificHolidayAsync(DateOnly date, bool tracking = false, CancellationToken cancellationToken = default);
    Task<SchoolHoliday?> GetRecurringHolidayAsync(int month, int day, bool tracking = false, CancellationToken cancellationToken = default);
    Task<SchoolHoliday?> GetHolidayByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<SchoolHoliday?> GetHolidayForDateAsync(DateOnly date, bool tracking = false, CancellationToken cancellationToken = default);
    Task<SchoolHoliday?> GetHolidayAsync(DateOnly date, bool tracking = false, CancellationToken cancellationToken = default);
    Task<bool> IsHolidayAsync(DateOnly date, CancellationToken cancellationToken = default);
    void AddHoliday(SchoolHoliday holiday);
    void RemoveHoliday(SchoolHoliday holiday);
    Task RemoveAutoHolidayRecordsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyAttendance>> GetDailyRecordsByStatusAsync(
        int academicYearId,
        DateOnly from,
        DateOnly to,
        AttendanceStatus status,
        IReadOnlyCollection<int>? sectionIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetMarkedDayCountsAsync(
        int academicYearId,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? sectionIds,
        CancellationToken cancellationToken = default);
}

