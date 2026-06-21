using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Services;

public class AttendanceService(
    ISchoolRepository schoolRepository,
    IAcademicYearRepository academicYearRepository,
    IStudentRepository studentRepository,
    IAttendanceRepository attendanceRepository,
    IUserAccessService userAccessService,
    IAttendanceNotificationService notificationService,
    IUnitOfWork unitOfWork) : IAttendanceService
{
    public async Task<DailyAttendanceSummaryDto> GetDailySummaryAsync(DateOnly date, string? userId = null, CancellationToken cancellationToken = default)
    {
        var rows = await GetDailyAttendanceRowsAsync(date, userId, cancellationToken);
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var holidayInfo = await ResolveHolidayInfoAsync(date, school, cancellationToken);

        return new DailyAttendanceSummaryDto(
            date,
            rows.Count,
            rows.Count(x => x.Status is not null),
            rows.Count(x => x.Status == AttendanceStatus.Present),
            rows.Count(x => x.Status == AttendanceStatus.Absent),
            rows.Count(x => x.Status == AttendanceStatus.Late),
            rows.Count(x => x.Status == AttendanceStatus.Leave),
            rows.Count(x => x.Status == AttendanceStatus.Holiday),
            rows.Count(x => x.Status is null),
            holidayInfo.IsNonWorkingDay,
            holidayInfo.Title);
    }

    public async Task<IReadOnlyList<DailyAttendanceRowDto>> GetDailyAttendanceRowsAsync(DateOnly date, string? userId = null, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var enrolled = await attendanceRepository.GetActiveStudentsAsync(currentYear.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(userId) && !await userAccessService.IsAdminAsync(userId, cancellationToken))
        {
            var allowed = (await userAccessService.GetAllowedSectionIdsAsync(userId, cancellationToken)).ToHashSet();
            enrolled = enrolled.Where(x => allowed.Contains(x.SectionId)).ToList();
        }

        var records = await attendanceRepository.GetDailyRecordsAsync(date, cancellationToken);
        var recordMap = records.ToDictionary(x => x.StudentId);
        var holidayInfo = await ResolveHolidayInfoAsync(date, school, cancellationToken);

        return enrolled
            .OrderBy(x => x.Section.ClassRoom.DisplayOrder)
            .ThenBy(x => x.Section.ClassRoom.Name)
            .ThenBy(x => x.Section.Name)
            .ThenBy(x => x.RollNumber)
            .Select(x =>
            {
                recordMap.TryGetValue(x.StudentId, out var record);
                return new DailyAttendanceRowDto(
                    x.StudentId,
                    x.RollNumber,
                    x.Student.FirstName + " " + x.Student.LastName,
                    x.Section.ClassRoom.Name + "-" + x.Section.Name,
                    record?.Status ?? (holidayInfo.IsNonWorkingDay ? AttendanceStatus.Holiday : null),
                    record?.CheckInTime,
                    record?.CheckOutTime,
                    record?.Remarks ?? (holidayInfo.IsNonWorkingDay ? holidayInfo.Title : null));
            })
            .ToList();
    }

    public async Task<ManualAttendanceSheetResultDto> GetManualAttendanceSheetAsync(int sectionId, DateOnly date, string? userId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSectionAccessAsync(sectionId, userId, cancellationToken);
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var students = await attendanceRepository.GetSectionStudentsAsync(sectionId, currentYear.Id, cancellationToken);
        var existing = await attendanceRepository.GetDailyRecordsBySectionAsync(sectionId, date, cancellationToken);
        var holidayInfo = await ResolveHolidayInfoAsync(date, school, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var blockReason = holidayInfo.IsNonWorkingDay
            ? "Attendance cannot be edited on holidays or weekly off days."
            : date > today
                ? "Future dates cannot be edited."
                : null;

        var rows = students.Select(x =>
        {
            if (existing.TryGetValue(x.StudentId, out var record))
            {
                return new ManualAttendanceRowDto
                {
                    StudentId = x.StudentId,
                    RollNumber = x.RollNumber,
                    StudentName = x.Student.FirstName + " " + x.Student.LastName,
                    Status = record.Status,
                    Remarks = record.Remarks
                };
            }

            return new ManualAttendanceRowDto
            {
                StudentId = x.StudentId,
                RollNumber = x.RollNumber,
                StudentName = x.Student.FirstName + " " + x.Student.LastName,
                Status = holidayInfo.IsNonWorkingDay ? AttendanceStatus.Holiday : AttendanceStatus.Absent,
                Remarks = holidayInfo.Title
            };
        }).ToList();

        return new ManualAttendanceSheetResultDto(rows, blockReason is null, blockReason);
    }
    public async Task SaveManualAttendanceAsync(int sectionId, DateOnly date, IReadOnlyList<ManualAttendanceRowDto> rows, string? userId, CancellationToken cancellationToken = default)
    {
        await EnsureSectionAccessAsync(sectionId, userId, cancellationToken);
        var sheet = await GetManualAttendanceSheetAsync(sectionId, date, userId, cancellationToken);
        if (!sheet.CanEdit)
        {
            throw new InvalidOperationException(sheet.BlockReason ?? "Attendance cannot be saved for this date.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);

        foreach (var row in rows)
        {
            var record = await attendanceRepository.GetDailyRecordAsync(row.StudentId, date, tracking: true, cancellationToken);

            if (record is null)
            {
                record = new DailyAttendance
                {
                    SchoolId = school.Id,
                    StudentId = row.StudentId,
                    AcademicYearId = currentYear.Id,
                    SectionId = sectionId,
                    AttendanceDate = date
                };
                attendanceRepository.AddDailyRecord(record);
            }

            record.Status = row.Status;
            record.Remarks = row.Remarks;
            record.IsManualEntry = true;
            record.UpdatedByUserId = userId;
            record.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceLogDto>> GetRecentLogsAsync(int take = 50, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var logs = date is null
            ? await attendanceRepository.GetRecentLogsAsync(take, cancellationToken)
            : await attendanceRepository.GetRecentLogsForDateAsync(date.Value, take, cancellationToken);

        var studentIds = logs.Select(x => x.StudentId).Distinct().ToList();
        var enrollments = await studentRepository.GetEnrollmentsForStudentsAsync(studentIds, currentYear.Id, cancellationToken);
        var enrollmentMap = enrollments.ToDictionary(x => x.StudentId);

        return logs.Select(log =>
        {
            enrollmentMap.TryGetValue(log.StudentId, out var enrollment);
            return new AttendanceLogDto(
                log.Id,
                log.Student.FirstName + " " + log.Student.LastName,
                enrollment?.RollNumber ?? "-",
                enrollment is null ? "-" : enrollment.Section.ClassRoom.Name + "-" + enrollment.Section.Name,
                log.ScanTime,
                log.Direction,
                log.ScanType,
                log.BiometricDeviceId,
                log.BiometricDevice?.Name);
        }).ToList();
    }

    public async Task<bool> ProcessBiometricScanAsync(
        string biometricUserId,
        int deviceId,
        ScanDirection? direction = null,
        CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        var map = await attendanceRepository.GetBiometricMapByDeviceAsync(deviceId, biometricUserId, cancellationToken);
        if (map is null)
        {
            return false;
        }

        var device = await attendanceRepository.GetDeviceByIdAsync(deviceId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Device not found.");

        var duplicateWindow = TimeSpan.FromSeconds(device.DuplicateScanBlockSeconds);
        var recent = await attendanceRepository.GetLatestLogAsync(map.StudentId, deviceId, cancellationToken);

        if (recent is not null && now - recent.ScanTime < duplicateWindow)
        {
            return false;
        }

        var daily = await attendanceRepository.GetDailyRecordAsync(map.StudentId, today, tracking: true, cancellationToken);
        var resolvedDirection = direction ?? ResolveScanDirection(daily, now);

        var scanType = device.BiometricType == BiometricType.Both
            ? map.BiometricType
            : device.BiometricType;

        attendanceRepository.AddAttendanceLog(new AttendanceLog
        {
            SchoolId = school.Id,
            StudentId = map.StudentId,
            BiometricDeviceId = deviceId,
            AttendanceDate = today,
            ScanTime = now,
            Direction = resolvedDirection,
            ScanType = scanType,
            Source = "Biometric"
        });

        device.LastScanAt = now;
        device.UpdatedAt = DateTime.UtcNow;

        var enrollment = await studentRepository.GetEnrollmentAsync(map.StudentId, currentYear.Id, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Enrollment not found.");

        if (daily is null)
        {
            daily = new DailyAttendance
            {
                SchoolId = school.Id,
                StudentId = map.StudentId,
                AcademicYearId = currentYear.Id,
                SectionId = enrollment.SectionId,
                AttendanceDate = today
            };
            attendanceRepository.AddDailyRecord(daily);
        }

        var notifyLate = false;
        if (resolvedDirection == ScanDirection.In)
        {
            daily.CheckInTime ??= now;
            var wasLate = daily.Status == AttendanceStatus.Late;
            daily.Status = IsLate(school, now) ? AttendanceStatus.Late : AttendanceStatus.Present;
            notifyLate = !wasLate && daily.Status == AttendanceStatus.Late;
        }
        else
        {
            daily.CheckOutTime = now;
            if (daily.Status == AttendanceStatus.Absent)
            {
                daily.Status = AttendanceStatus.Present;
            }
        }

        daily.IsManualEntry = false;
        daily.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (notifyLate)
        {
            await notificationService.QueueLateNotificationAsync(map.StudentId, today, cancellationToken);
        }

        return true;
    }

    public async Task FinalizeDailyAttendanceAsync(DateOnly date, string? userId = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(userId) && !await userAccessService.HasFullAttendanceAccessAsync(userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Only coordinator or admin can finalize attendance for all students.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var holidayInfo = await ResolveHolidayInfoAsync(date, school, cancellationToken);
        if (holidayInfo.IsNonWorkingDay)
        {
            return;
        }

        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);

        var activeStudents = await attendanceRepository.GetActiveStudentsAsync(currentYear.Id, cancellationToken);
        var existing = await attendanceRepository.GetStudentIdsWithDailyRecordAsync(date, cancellationToken);

        foreach (var student in activeStudents.Where(x => !existing.Contains(x.StudentId)))
        {
            attendanceRepository.AddDailyRecord(new DailyAttendance
            {
                SchoolId = school.Id,
                StudentId = student.StudentId,
                AcademicYearId = currentYear.Id,
                SectionId = student.SectionId,
                AttendanceDate = date,
                Status = AttendanceStatus.Absent,
                IsManualEntry = false
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationService.QueueAbsentNotificationsAsync(date, cancellationToken);
    }

    public async Task<MonthlyRegisterDto> GetMonthlyRegisterAsync(int sectionId, int year, int month, string? userId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSectionAccessAsync(sectionId, userId, cancellationToken);
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var students = await attendanceRepository.GetSectionStudentsAsync(sectionId, currentYear.Id, cancellationToken);
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var records = await attendanceRepository.GetDailyRecordsForSectionAsync(sectionId, firstDay, lastDay, cancellationToken);
        var recordLookup = records.ToDictionary(x => (x.StudentId, x.AttendanceDate));

        var dates = new List<DateOnly>();
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        var holidayCache = new Dictionary<DateOnly, HolidayInfo>();
        async Task<HolidayInfo> GetHoliday(DateOnly date)
        {
            if (!holidayCache.TryGetValue(date, out var info))
            {
                info = await ResolveHolidayInfoAsync(date, school, cancellationToken);
                holidayCache[date] = info;
            }

            return info;
        }

        var sectionName = students.Count > 0
            ? $"{students[0].Section.ClassRoom.Name}-{students[0].Section.Name}"
            : "Section";
        var rows = new List<MonthlyRegisterStudentRowDto>();
        foreach (var student in students)
        {
            var cells = new List<MonthlyRegisterCellDto>();
            foreach (var date in dates)
            {
                var holiday = await GetHoliday(date);
                recordLookup.TryGetValue((student.StudentId, date), out var record);
                cells.Add(new MonthlyRegisterCellDto(
                    date,
                    record?.Status ?? (holiday.IsNonWorkingDay ? AttendanceStatus.Holiday : null),
                    holiday.IsNonWorkingDay));
            }

            rows.Add(new MonthlyRegisterStudentRowDto(
                student.RollNumber,
                student.Student.FirstName + " " + student.Student.LastName,
                cells));
        }

        return new MonthlyRegisterDto(year, month, sectionId, sectionName, dates, rows);
    }

    public async Task<AttendanceCalendarMonthDto> GetAttendanceCalendarAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var firstOfMonth = new DateOnly(year, month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        var gridStart = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
        var gridEnd = lastOfMonth.AddDays(6 - (int)lastOfMonth.DayOfWeek);
        var holidays = await attendanceRepository.GetHolidaysAsync(gridStart, gridEnd, cancellationToken);
        var specificMap = holidays.ToDictionary(x => x.HolidayDate, x => x);
        var recurringHolidays = await attendanceRepository.GetRecurringHolidaysAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = new List<AttendanceCalendarDayDto>();

        for (var date = gridStart; date <= gridEnd; date = date.AddDays(1))
        {
            specificMap.TryGetValue(date, out var specificHoliday);
            var recurringHoliday = specificHoliday is null
                ? recurringHolidays.FirstOrDefault(x => AnnualHolidayRules.MatchesDate(x.RepeatsAnnually, x.RecurringMonth, x.RecurringDay, date))
                : null;
            var matchedHoliday = specificHoliday ?? recurringHoliday;
            var isManualHoliday = matchedHoliday is not null;
            var isAnnualRecurring = recurringHoliday is not null;
            var isWeeklyOff = matchedHoliday is null && WeeklyOffRules.IsOffDay(date.DayOfWeek, school.WeeklyOffDays);
            var title = matchedHoliday?.Title
                ?? (isWeeklyOff ? WeeklyOffRules.GetOffDayTitle(date.DayOfWeek) : null);

            days.Add(new AttendanceCalendarDayDto(
                date,
                date.Month == month,
                date == today,
                isManualHoliday || isWeeklyOff,
                title,
                isWeeklyOff,
                isManualHoliday,
                isAnnualRecurring));
        }

        return new AttendanceCalendarMonthDto(year, month, days);
    }

    public async Task<IReadOnlyList<SchoolHolidayDto>> GetAnnualHolidaysAsync(CancellationToken cancellationToken = default)
    {
        var holidays = await attendanceRepository.GetRecurringHolidaysAsync(cancellationToken);
        return holidays
            .Select(x => new SchoolHolidayDto(
                x.Id,
                x.HolidayDate,
                x.Title,
                x.Description,
                x.RepeatsAnnually,
                x.RecurringMonth,
                x.RecurringDay))
            .ToList();
    }

    public async Task MarkHolidayAsync(
        DateOnly date,
        string title,
        string? description = null,
        bool repeatsAnnually = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Holiday title is required.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var trimmedTitle = title.Trim();
        var trimmedDescription = description?.Trim();

        SchoolHoliday holiday;
        if (repeatsAnnually)
        {
            var existingSpecific = await attendanceRepository.GetSpecificHolidayAsync(date, tracking: true, cancellationToken);
            if (existingSpecific is not null)
            {
                attendanceRepository.RemoveHoliday(existingSpecific);
            }

            holiday = await attendanceRepository.GetRecurringHolidayAsync(date.Month, date.Day, tracking: true, cancellationToken)
                ?? new SchoolHoliday { SchoolId = school.Id };

            holiday.Title = trimmedTitle;
            holiday.Description = trimmedDescription;
            holiday.RepeatsAnnually = true;
            holiday.HolidayDate = date;
            holiday.RecurringMonth = date.Month;
            holiday.RecurringDay = date.Day;
            holiday.UpdatedAt = DateTime.UtcNow;

            if (holiday.Id == 0)
            {
                attendanceRepository.AddHoliday(holiday);
            }
        }
        else
        {
            holiday = await attendanceRepository.GetSpecificHolidayAsync(date, tracking: true, cancellationToken)
                ?? new SchoolHoliday { SchoolId = school.Id };

            holiday.Title = trimmedTitle;
            holiday.Description = trimmedDescription;
            holiday.RepeatsAnnually = false;
            holiday.HolidayDate = date;
            holiday.RecurringMonth = date.Month;
            holiday.RecurringDay = date.Day;
            holiday.UpdatedAt = DateTime.UtcNow;

            if (holiday.Id == 0)
            {
                attendanceRepository.AddHoliday(holiday);
            }
        }

        await ApplyHolidayRecordsForDateAsync(school, currentYear, date, trimmedTitle, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveHolidayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var specific = await attendanceRepository.GetSpecificHolidayAsync(date, tracking: true, cancellationToken);
        if (specific is not null)
        {
            attendanceRepository.RemoveHoliday(specific);
            await attendanceRepository.RemoveAutoHolidayRecordsAsync(date, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var recurring = await attendanceRepository.GetRecurringHolidayAsync(date.Month, date.Day, tracking: true, cancellationToken);
        if (recurring is null)
        {
            return;
        }

        attendanceRepository.RemoveHoliday(recurring);
        await attendanceRepository.RemoveAutoHolidayRecordsAsync(date, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAnnualHolidayAsync(int holidayId, CancellationToken cancellationToken = default)
    {
        var holiday = await attendanceRepository.GetHolidayByIdAsync(holidayId, tracking: true, cancellationToken);
        if (holiday is null || !holiday.RepeatsAnnually)
        {
            return;
        }

        attendanceRepository.RemoveHoliday(holiday);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyHolidayRecordsForDateAsync(
        Domain.Entities.Shared.School school,
        Domain.Entities.Shared.AcademicYear currentYear,
        DateOnly date,
        string title,
        CancellationToken cancellationToken)
    {
        var students = await attendanceRepository.GetActiveStudentsAsync(currentYear.Id, cancellationToken);
        foreach (var student in students)
        {
            var record = await attendanceRepository.GetDailyRecordAsync(student.StudentId, date, tracking: true, cancellationToken);
            if (record is null)
            {
                attendanceRepository.AddDailyRecord(new DailyAttendance
                {
                    SchoolId = school.Id,
                    StudentId = student.StudentId,
                    AcademicYearId = currentYear.Id,
                    SectionId = student.SectionId,
                    AttendanceDate = date,
                    Status = AttendanceStatus.Holiday,
                    Remarks = title,
                    IsManualEntry = false
                });
                continue;
            }

            if (record.Status is AttendanceStatus.Absent or AttendanceStatus.Holiday)
            {
                record.Status = AttendanceStatus.Holiday;
                record.Remarks = title;
                record.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private sealed record HolidayInfo(
        bool IsNonWorkingDay,
        string? Title,
        bool IsWeeklyOff,
        bool IsManualHoliday,
        bool IsAnnualRecurring);

    private async Task<HolidayInfo> ResolveHolidayInfoAsync(
        DateOnly date,
        Domain.Entities.Shared.School school,
        CancellationToken cancellationToken)
    {
        var specific = await attendanceRepository.GetSpecificHolidayAsync(date, cancellationToken: cancellationToken);
        if (specific is not null)
        {
            return new HolidayInfo(true, specific.Title, false, true, false);
        }

        var recurring = await attendanceRepository.GetRecurringHolidayAsync(date.Month, date.Day, cancellationToken: cancellationToken);
        if (recurring is not null)
        {
            return new HolidayInfo(true, recurring.Title, false, true, true);
        }

        if (WeeklyOffRules.IsOffDay(date.DayOfWeek, school.WeeklyOffDays))
        {
            return new HolidayInfo(true, WeeklyOffRules.GetOffDayTitle(date.DayOfWeek), true, false, false);
        }

        return new HolidayInfo(false, null, false, false, false);
    }

    private async Task EnsureSectionAccessAsync(int sectionId, string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId) || await userAccessService.HasFullAttendanceAccessAsync(userId, cancellationToken))
        {
            return;
        }

        var allowed = await userAccessService.GetAllowedSectionIdsAsync(userId, cancellationToken);
        if (!allowed.Contains(sectionId))
        {
            throw new UnauthorizedAccessException("You do not have access to this section.");
        }
    }

    private static ScanDirection ResolveScanDirection(DailyAttendance? daily, DateTime scanTime)
    {
        if (daily?.CheckInTime is null)
        {
            return ScanDirection.In;
        }

        if (daily.CheckOutTime is null)
        {
            return ScanDirection.Out;
        }

        return ScanDirection.Out;
    }

    private static bool IsLate(Domain.Entities.Shared.School school, DateTime scanTime)
    {
        var start = school.SchoolStartTime.AddMinutes(school.LateAfterMinutes);
        var scan = TimeOnly.FromDateTime(scanTime);
        return scan > start;
    }

    public async Task<IReadOnlyList<AttendancePatternStudentDto>> GetAttendancePatternAsync(
        DateOnly from,
        DateOnly to,
        AttendanceStatus status,
        int minOccurrences = 3,
        int minConsecutive = 0,
        int? sectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (status == AttendanceStatus.Holiday)
        {
            throw new InvalidOperationException("Holiday is not supported for pattern reports.");
        }

        if (to < from)
        {
            throw new InvalidOperationException("End date must be on or after start date.");
        }

        minOccurrences = Math.Max(1, minOccurrences);
        minConsecutive = Math.Max(0, minConsecutive);

        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        IReadOnlyCollection<int>? sectionIds = null;

        if (sectionId is > 0)
        {
            await EnsureSectionAccessAsync(sectionId.Value, userId, cancellationToken);
            sectionIds = [sectionId.Value];
        }
        else if (!string.IsNullOrWhiteSpace(userId) && !await userAccessService.HasFullAttendanceAccessAsync(userId, cancellationToken))
        {
            var allowed = await userAccessService.GetAllowedSectionIdsAsync(userId, cancellationToken);
            sectionIds = allowed.Count > 0 ? allowed : [];
        }

        var statusRecords = await attendanceRepository.GetDailyRecordsByStatusAsync(
            currentYear.Id,
            from,
            to,
            status,
            sectionIds,
            cancellationToken);
        var markedCounts = await attendanceRepository.GetMarkedDayCountsAsync(
            currentYear.Id,
            from,
            to,
            sectionIds,
            cancellationToken);
        var enrollments = await attendanceRepository.GetActiveStudentsAsync(currentYear.Id, cancellationToken);
        var enrollmentMap = enrollments.ToDictionary(x => x.StudentId);

        var results = new List<AttendancePatternStudentDto>();

        foreach (var group in statusRecords.GroupBy(x => x.StudentId))
        {
            var occurrenceDates = group.Select(x => x.AttendanceDate).OrderBy(x => x).ToList();
            var occurrenceCount = occurrenceDates.Count;
            if (occurrenceCount < minOccurrences)
            {
                continue;
            }

            var longestStreak = GetLongestConsecutiveStreak(occurrenceDates);
            var currentStreak = GetCurrentConsecutiveStreak(occurrenceDates);
            if (minConsecutive > 0 && longestStreak < minConsecutive)
            {
                continue;
            }

            if (!enrollmentMap.TryGetValue(group.Key, out var enrollment))
            {
                continue;
            }

            results.Add(new AttendancePatternStudentDto(
                group.Key,
                enrollment.RollNumber,
                enrollment.Student.FirstName + " " + enrollment.Student.LastName,
                enrollment.Section.ClassRoom.Name + "-" + enrollment.Section.Name,
                status,
                occurrenceCount,
                markedCounts.GetValueOrDefault(group.Key, 0),
                longestStreak,
                currentStreak,
                occurrenceDates[^1]));
        }

        return results
            .OrderByDescending(x => x.OccurrenceCount)
            .ThenByDescending(x => x.LongestStreak)
            .ThenBy(x => x.StudentName)
            .ToList();
    }

    private static int GetLongestConsecutiveStreak(IReadOnlyList<DateOnly> orderedDates)
    {
        if (orderedDates.Count == 0)
        {
            return 0;
        }

        var max = 1;
        var current = 1;
        for (var i = 1; i < orderedDates.Count; i++)
        {
            current = orderedDates[i].DayNumber == orderedDates[i - 1].DayNumber + 1
                ? current + 1
                : 1;
            max = Math.Max(max, current);
        }

        return max;
    }

    private static int GetCurrentConsecutiveStreak(IReadOnlyList<DateOnly> orderedDates)
    {
        if (orderedDates.Count == 0)
        {
            return 0;
        }

        var streak = 1;
        for (var i = orderedDates.Count - 1; i > 0; i--)
        {
            if (orderedDates[i].DayNumber == orderedDates[i - 1].DayNumber + 1)
            {
                streak++;
            }
            else
            {
                break;
            }
        }

        return streak;
    }
}

