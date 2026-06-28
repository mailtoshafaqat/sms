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
