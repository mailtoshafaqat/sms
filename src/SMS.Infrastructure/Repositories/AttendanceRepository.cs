using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class AttendanceRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IAttendanceRepository
{
    public Task<IReadOnlyList<DailyAttendance>> GetDailyRecordsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<DailyAttendance>)await db.DailyAttendances.AsNoTracking()
                .Where(x => x.AttendanceDate == date)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<StudentEnrollment>> GetSectionStudentsAsync(int sectionId, int academicYearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<StudentEnrollment>)await db.StudentEnrollments.AsNoTracking()
                .Include(x => x.Student)
                .Include(x => x.Section)
                .ThenInclude(x => x.ClassRoom)
                .Where(x => x.SectionId == sectionId && x.AcademicYearId == academicYearId && x.IsActive && x.Student.IsActive)
                .OrderBy(x => x.RollNumber)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<Dictionary<int, DailyAttendance>> GetDailyRecordsBySectionAsync(int sectionId, DateOnly date, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.DailyAttendances.AsNoTracking()
                .Where(x => x.SectionId == sectionId && x.AttendanceDate == date)
                .ToDictionaryAsync(x => x.StudentId, cancellationToken),
            cancellationToken);

    public Task<DailyAttendance?> GetDailyRecordAsync(int studentId, DateOnly date, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.DailyAttendances.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.StudentId == studentId && x.AttendanceDate == date, cancellationToken);
            },
            cancellationToken);

    public void AddDailyRecord(DailyAttendance record) => scopedDb.Context.DailyAttendances.Add(record);

    public Task<IReadOnlyList<AttendanceLog>> GetRecentLogsAsync(int take, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<AttendanceLog>)await db.AttendanceLogs.AsNoTracking()
                .Include(x => x.Student)
                .Include(x => x.BiometricDevice)
                .OrderByDescending(x => x.ScanTime)
                .Take(take)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<AttendanceLog>> GetRecentLogsForDateAsync(DateOnly date, int take, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<AttendanceLog>)await db.AttendanceLogs.AsNoTracking()
                .Include(x => x.Student)
                .Include(x => x.BiometricDevice)
                .Where(x => x.AttendanceDate == date)
                .OrderByDescending(x => x.ScanTime)
                .Take(take)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<DailyAttendance>> GetDailyRecordsForSectionAsync(int sectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<DailyAttendance>)await db.DailyAttendances.AsNoTracking()
                .Where(x => x.SectionId == sectionId && x.AttendanceDate >= from && x.AttendanceDate <= to)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<StudentBiometricMap?> GetBiometricMapByDeviceAsync(int deviceId, string biometricUserId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentBiometricMaps.AsNoTracking()
                .Include(x => x.Student)
                .FirstOrDefaultAsync(x => x.BiometricDeviceId == deviceId && x.BiometricUserId == biometricUserId, cancellationToken),
            cancellationToken);

    public Task<BiometricDevice?> GetDeviceByIdAsync(int deviceId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.BiometricDevices.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
            },
            cancellationToken);

    public Task<AttendanceLog?> GetLatestLogAsync(int studentId, int deviceId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.AttendanceLogs.AsNoTracking()
                .Where(x => x.StudentId == studentId && x.BiometricDeviceId == deviceId)
                .OrderByDescending(x => x.ScanTime)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);

    public void AddAttendanceLog(AttendanceLog log) => scopedDb.Context.AttendanceLogs.Add(log);

    public Task<IReadOnlyList<StudentEnrollment>> GetActiveStudentsAsync(int academicYearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<StudentEnrollment>)await db.StudentEnrollments.AsNoTracking()
                .Include(x => x.Student)
                .Include(x => x.Section)
                .ThenInclude(x => x.ClassRoom)
                .Where(x => x.AcademicYearId == academicYearId && x.IsActive && x.Student.IsActive)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<HashSet<int>> GetStudentIdsWithDailyRecordAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var ids = await db.DailyAttendances.AsNoTracking()
                    .Where(x => x.AttendanceDate == date)
                    .Select(x => x.StudentId)
                    .ToListAsync(cancellationToken);

                return ids.ToHashSet();
            },
            cancellationToken);

    public Task<IReadOnlyList<SchoolHoliday>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<SchoolHoliday>)await db.SchoolHolidays.AsNoTracking()
                .Where(x => !x.RepeatsAnnually && x.HolidayDate >= from && x.HolidayDate <= to)
                .OrderBy(x => x.HolidayDate)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<SchoolHoliday>> GetRecurringHolidaysAsync(CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<SchoolHoliday>)await db.SchoolHolidays.AsNoTracking()
                .Where(x => x.RepeatsAnnually)
                .OrderBy(x => x.RecurringMonth)
                .ThenBy(x => x.RecurringDay)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<SchoolHoliday?> GetSpecificHolidayAsync(DateOnly date, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.SchoolHolidays.Where(x => !x.RepeatsAnnually && x.HolidayDate == date);
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(cancellationToken);
            },
            cancellationToken);

    public Task<SchoolHoliday?> GetRecurringHolidayAsync(int month, int day, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.SchoolHolidays.Where(x =>
                    x.RepeatsAnnually &&
                    x.RecurringMonth == month &&
                    x.RecurringDay == day);
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(cancellationToken);
            },
            cancellationToken);

    public Task<SchoolHoliday?> GetHolidayByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.SchoolHolidays.Where(x => x.Id == id);
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(cancellationToken);
            },
            cancellationToken);

    public async Task<SchoolHoliday?> GetHolidayForDateAsync(DateOnly date, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var specific = await GetSpecificHolidayAsync(date, tracking, cancellationToken);
        if (specific is not null)
        {
            return specific;
        }

        return await GetRecurringHolidayAsync(date.Month, date.Day, tracking, cancellationToken);
    }

    public Task<SchoolHoliday?> GetHolidayAsync(DateOnly date, bool tracking = false, CancellationToken cancellationToken = default) =>
        GetHolidayForDateAsync(date, tracking, cancellationToken);

    public Task<bool> IsHolidayAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.SchoolHolidays.AsNoTracking().AnyAsync(
                x =>
                    (!x.RepeatsAnnually && x.HolidayDate == date) ||
                    (x.RepeatsAnnually && x.RecurringMonth == date.Month && x.RecurringDay == date.Day),
                cancellationToken),
            cancellationToken);

    public void AddHoliday(SchoolHoliday holiday) => scopedDb.Context.SchoolHolidays.Add(holiday);

    public void RemoveHoliday(SchoolHoliday holiday) => scopedDb.Context.SchoolHolidays.Remove(holiday);

    public async Task RemoveAutoHolidayRecordsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var records = await scopedDb.Context.DailyAttendances
            .Where(x =>
                x.AttendanceDate == date &&
                x.Status == AttendanceStatus.Holiday &&
                !x.IsManualEntry &&
                x.CheckInTime == null)
            .ToListAsync(cancellationToken);

        scopedDb.Context.DailyAttendances.RemoveRange(records);
    }

    public Task<IReadOnlyList<DailyAttendance>> GetDailyRecordsByStatusAsync(
        int academicYearId,
        DateOnly from,
        DateOnly to,
        AttendanceStatus status,
        IReadOnlyCollection<int>? sectionIds,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = db.DailyAttendances.AsNoTracking()
                    .Where(x =>
                        x.AcademicYearId == academicYearId
                        && x.AttendanceDate >= from
                        && x.AttendanceDate <= to
                        && x.Status == status);

                if (sectionIds is { Count: > 0 })
                {
                    query = query.Where(x => sectionIds.Contains(x.SectionId));
                }

                return (IReadOnlyList<DailyAttendance>)await query.ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetMarkedDayCountsAsync(
        int academicYearId,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? sectionIds,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = db.DailyAttendances.AsNoTracking()
                    .Where(x =>
                        x.AcademicYearId == academicYearId
                        && x.AttendanceDate >= from
                        && x.AttendanceDate <= to
                        && x.Status != AttendanceStatus.Holiday);

                if (sectionIds is { Count: > 0 })
                {
                    query = query.Where(x => sectionIds.Contains(x.SectionId));
                }

                return (IReadOnlyDictionary<int, int>)await query
                    .GroupBy(x => x.StudentId)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
            },
            cancellationToken);
}
