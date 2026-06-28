using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Services;

public class StaffAttendanceService(
    ISchoolRepository schoolRepository,
    IClassRepository classRepository,
    IAttendanceRepository attendanceRepository,
    IStaffAttendanceRepository staffAttendanceRepository,
    IUnitOfWork unitOfWork) : IStaffAttendanceService
{
    public async Task<StaffAttendanceSheetResultDto> GetSheetAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var teachers = await classRepository.GetTeachersAsync(school.Id, cancellationToken);
        var activeTeachers = teachers.Where(x => x.IsActive).OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToList();
        var existing = (await staffAttendanceRepository.GetByDateAsync(school.Id, date, cancellationToken))
            .ToDictionary(x => x.TeacherId);

        var holidayInfo = await ResolveHolidayInfoAsync(date, school, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var blockReason = holidayInfo.IsNonWorkingDay
            ? "Staff attendance cannot be edited on holidays or weekly off days."
            : date > today
                ? "Future dates cannot be edited."
                : null;

        var rows = activeTeachers.Select(teacher =>
        {
            if (existing.TryGetValue(teacher.Id, out var record))
            {
                return new StaffAttendanceRowDto
                {
                    TeacherId = teacher.Id,
                    EmployeeCode = teacher.EmployeeCode,
                    StaffName = teacher.FullName,
                    Status = record.Status,
                    Remarks = record.Remarks
                };
            }

            return new StaffAttendanceRowDto
            {
                TeacherId = teacher.Id,
                EmployeeCode = teacher.EmployeeCode,
                StaffName = teacher.FullName,
                Status = holidayInfo.IsNonWorkingDay ? AttendanceStatus.Holiday : AttendanceStatus.Present,
                Remarks = holidayInfo.IsNonWorkingDay ? holidayInfo.Title : null
            };
        }).ToList();

        return new StaffAttendanceSheetResultDto(rows, blockReason is null, blockReason);
    }

    public async Task SaveSheetAsync(DateOnly date, IReadOnlyList<StaffAttendanceRowDto> rows, string? userId, CancellationToken cancellationToken = default)
    {
        var sheet = await GetSheetAsync(date, cancellationToken);
        if (!sheet.CanEdit)
        {
            throw new InvalidOperationException(sheet.BlockReason ?? "Staff attendance cannot be edited for this date.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        foreach (var row in rows)
        {
            var record = await staffAttendanceRepository.GetByTeacherAndDateAsync(row.TeacherId, date, tracking: true, cancellationToken);
            if (record is null)
            {
                record = new StaffDailyAttendance
                {
                    SchoolId = school.Id,
                    TeacherId = row.TeacherId,
                    AttendanceDate = date,
                    IsManualEntry = true
                };
                staffAttendanceRepository.Add(record);
            }

            record.Status = row.Status;
            record.Remarks = string.IsNullOrWhiteSpace(row.Remarks) ? null : row.Remarks.Trim();
            record.IsManualEntry = true;
            record.UpdatedByUserId = userId;
            record.UpdatedAt = DateTime.UtcNow;

            if (row.Status is AttendanceStatus.Present or AttendanceStatus.Late && record.CheckInTime is null)
            {
                record.CheckInTime = DateTime.Now;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffMonthlyRegisterDto> GetMonthlyRegisterAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var teachers = await classRepository.GetTeachersAsync(school.Id, cancellationToken);
        var activeTeachers = teachers.Where(x => x.IsActive).OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToList();

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var records = await staffAttendanceRepository.GetByDateRangeAsync(school.Id, firstDay, lastDay, cancellationToken);
        var recordLookup = records.ToDictionary(x => (x.TeacherId, x.AttendanceDate));

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

        var rows = new List<StaffMonthlyRegisterRowDto>();
        foreach (var teacher in activeTeachers)
        {
            var cells = new List<MonthlyRegisterCellDto>();
            foreach (var date in dates)
            {
                var holiday = await GetHoliday(date);
                recordLookup.TryGetValue((teacher.Id, date), out var record);
                cells.Add(new MonthlyRegisterCellDto(
                    date,
                    record?.Status ?? (holiday.IsNonWorkingDay ? AttendanceStatus.Holiday : null),
                    holiday.IsNonWorkingDay));
            }

            rows.Add(new StaffMonthlyRegisterRowDto(teacher.EmployeeCode, teacher.FullName, cells));
        }

        return new StaffMonthlyRegisterDto(year, month, dates, rows);
    }

    public async Task<StaffAttendanceSummaryResultDto> GetSummaryAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var teachers = await classRepository.GetTeachersAsync(school.Id, cancellationToken);
        var activeTeachers = teachers.Where(x => x.IsActive).OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToList();
        var records = await staffAttendanceRepository.GetByDateRangeAsync(school.Id, startDate, endDate, cancellationToken);
        var recordLookup = records.ToDictionary(x => (x.TeacherId, x.AttendanceDate));

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

        var dates = new List<DateOnly>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        var rows = new List<StaffAttendanceSummaryRowDto>();
        foreach (var teacher in activeTeachers)
        {
            var present = 0;
            var late = 0;
            var absent = 0;
            var leave = 0;
            var holiday = 0;
            var unmarked = 0;
            var workingDays = 0;

            foreach (var date in dates)
            {
                var holidayInfo = await GetHoliday(date);
                if (holidayInfo.IsNonWorkingDay)
                {
                    holiday++;
                    continue;
                }

                workingDays++;
                if (!recordLookup.TryGetValue((teacher.Id, date), out var record))
                {
                    unmarked++;
                    continue;
                }

                switch (record.Status)
                {
                    case AttendanceStatus.Present:
                        present++;
                        break;
                    case AttendanceStatus.Late:
                        late++;
                        break;
                    case AttendanceStatus.Absent:
                        absent++;
                        break;
                    case AttendanceStatus.Leave:
                        leave++;
                        break;
                    case AttendanceStatus.Holiday:
                        holiday++;
                        workingDays--;
                        break;
                    default:
                        unmarked++;
                        break;
                }
            }

            rows.Add(new StaffAttendanceSummaryRowDto(
                teacher.Id,
                teacher.EmployeeCode,
                teacher.FullName,
                workingDays,
                present,
                late,
                present + late,
                absent,
                leave,
                holiday,
                unmarked));
        }

        return new StaffAttendanceSummaryResultDto(startDate, endDate, rows);
    }

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

    private sealed record HolidayInfo(bool IsNonWorkingDay, string? Title, bool IsWeeklyOff, bool IsHoliday, bool IsAnnualHoliday);
}
