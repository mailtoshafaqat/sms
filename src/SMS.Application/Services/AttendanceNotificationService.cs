using SMS.Application.Common;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Application.Services;

public class AttendanceNotificationService(
    ISchoolRepository schoolRepository,
    IAcademicYearRepository academicYearRepository,
    IAttendanceRepository attendanceRepository,
    IAttendanceNotificationRepository notificationRepository,
    IStudentRepository studentRepository,
    IUnitOfWork unitOfWork) : IAttendanceNotificationService
{
    public Task QueueAbsentNotificationsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        QueueStatusNotificationsAsync(date, AttendanceStatus.Absent, cancellationToken);

    public Task QueueLateNotificationAsync(int studentId, DateOnly date, DateTime? eventTime = null, CancellationToken cancellationToken = default) =>
        QueueAttendanceNotificationAsync(studentId, date, AttendanceNotificationType.Late, eventTime, cancellationToken);

    public Task QueueCheckInNotificationAsync(int studentId, DateOnly date, DateTime scanTime, CancellationToken cancellationToken = default) =>
        QueueAttendanceNotificationAsync(studentId, date, AttendanceNotificationType.CheckIn, scanTime, cancellationToken);

    public Task QueueCheckOutNotificationAsync(int studentId, DateOnly date, DateTime scanTime, CancellationToken cancellationToken = default) =>
        QueueAttendanceNotificationAsync(studentId, date, AttendanceNotificationType.CheckOut, scanTime, cancellationToken);

    public async Task QueueAttendanceStatusNotificationAsync(
        int studentId,
        DateOnly date,
        AttendanceStatus status,
        CancellationToken cancellationToken = default)
    {
        var type = NotificationMessageBuilder.ToNotificationType(status);
        if (type is null)
        {
            return;
        }

        await QueueAttendanceNotificationAsync(studentId, date, type.Value, cancellationToken: cancellationToken);
    }

    public async Task QueueAttendanceNotificationAsync(
        int studentId,
        DateOnly date,
        AttendanceNotificationType type,
        DateTime? eventTime = null,
        CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        if (!IsEnabled(school, type))
        {
            return;
        }

        if (await notificationRepository.ExistsAsync(studentId, date, type, cancellationToken))
        {
            return;
        }

        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var enrollment = await studentRepository.GetEnrollmentAsync(studentId, currentYear.Id, cancellationToken: cancellationToken);
        if (enrollment is null)
        {
            return;
        }

        var phone = ResolvePhone(enrollment.Student.WhatsAppNumber, enrollment.Student.Phone);
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        var localTime = eventTime.HasValue ? TimeOnly.FromDateTime(eventTime.Value) : (TimeOnly?)null;
        var message = BuildMessage(
            school,
            type,
            enrollment.Student.FullName,
            date,
            enrollment.Section.ClassRoom.Name,
            enrollment.Section.Name,
            localTime);

        notificationRepository.Add(new AttendanceNotificationLog
        {
            SchoolId = school.Id,
            StudentId = studentId,
            AttendanceDate = date,
            NotificationType = type,
            RecipientPhone = phone,
            Message = message
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceNotificationDto>> GetNotificationsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var logs = await notificationRepository.GetByDateAsync(school.Id, date, cancellationToken);
        var results = new List<AttendanceNotificationDto>();

        foreach (var log in logs)
        {
            var enrollment = await studentRepository.GetEnrollmentAsync(log.StudentId, currentYear.Id, cancellationToken: cancellationToken);
            results.Add(new AttendanceNotificationDto(
                log.Id,
                log.StudentId,
                log.Student.FullName,
                enrollment?.RollNumber ?? "-",
                enrollment is null ? "-" : $"{enrollment.Section.ClassRoom.Name}-{enrollment.Section.Name}",
                log.AttendanceDate,
                GetDisplayType(log.NotificationType),
                log.RecipientPhone,
                log.Message,
                log.IsSent,
                log.SentAt,
                BuildWhatsAppUrl(log.RecipientPhone, log.Message)));
        }

        return results;
    }

    public async Task MarkSentAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var log = await notificationRepository.GetByIdAsync(notificationId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Notification not found.");

        log.IsSent = true;
        log.SentAt = DateTime.UtcNow;
        log.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public string BuildWhatsAppUrl(string phone, string message)
    {
        var normalized = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return $"https://wa.me/{normalized}?text={Uri.EscapeDataString(message)}";
    }

    private async Task QueueStatusNotificationsAsync(DateOnly date, AttendanceStatus status, CancellationToken cancellationToken)
    {
        var type = NotificationMessageBuilder.ToNotificationType(status);
        if (type is null)
        {
            return;
        }

        var records = (await attendanceRepository.GetDailyRecordsAsync(date, cancellationToken))
            .Where(x => x.Status == status)
            .ToList();

        foreach (var record in records)
        {
            await QueueAttendanceNotificationAsync(record.StudentId, date, type.Value, cancellationToken: cancellationToken);
        }
    }

    private static bool IsEnabled(School school, AttendanceNotificationType type) => type switch
    {
        AttendanceNotificationType.Absent => school.NotifyAbsent,
        AttendanceNotificationType.Late => school.NotifyLate,
        AttendanceNotificationType.CheckIn => school.NotifyCheckIn,
        AttendanceNotificationType.CheckOut => school.NotifyCheckOut,
        AttendanceNotificationType.Leave => school.NotifyLeave,
        AttendanceNotificationType.Present => school.NotifyPresent,
        _ => false
    };

    private static string BuildMessage(
        School school,
        AttendanceNotificationType type,
        string studentName,
        DateOnly date,
        string className,
        string sectionName,
        TimeOnly? time) => type switch
    {
        AttendanceNotificationType.Absent => NotificationMessageBuilder.BuildAbsentMessage(
            school.AbsentNotificationTemplate, school.Name, studentName, date, className, sectionName),
        AttendanceNotificationType.Late => NotificationMessageBuilder.BuildLateMessage(
            school.LateNotificationTemplate, school.Name, studentName, date, time),
        AttendanceNotificationType.CheckIn => NotificationMessageBuilder.BuildCheckInMessage(
            school.CheckInNotificationTemplate, school.Name, studentName, date, className, sectionName, time ?? TimeOnly.MinValue),
        AttendanceNotificationType.CheckOut => NotificationMessageBuilder.BuildCheckOutMessage(
            school.CheckOutNotificationTemplate, school.Name, studentName, date, time ?? TimeOnly.MinValue),
        AttendanceNotificationType.Leave => NotificationMessageBuilder.BuildLeaveMessage(
            school.LeaveNotificationTemplate, school.Name, studentName, date, className, sectionName),
        AttendanceNotificationType.Present => NotificationMessageBuilder.BuildPresentMessage(
            school.PresentNotificationTemplate, school.Name, studentName, date, className, sectionName),
        _ => string.Empty
    };

    private static string GetDisplayType(AttendanceNotificationType type) => type switch
    {
        AttendanceNotificationType.CheckIn => "Check in",
        AttendanceNotificationType.CheckOut => "Check out",
        AttendanceNotificationType.Leave => "Leave",
        AttendanceNotificationType.Present => "Present",
        _ => type.ToString()
    };

    private static string? ResolvePhone(string? whatsApp, string? phone) =>
        string.IsNullOrWhiteSpace(whatsApp) ? phone?.Trim() : whatsApp.Trim();

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("92"))
        {
            return digits;
        }

        if (digits.StartsWith('0') && digits.Length >= 11)
        {
            return "92" + digits[1..];
        }

        return digits.Length >= 10 ? "92" + digits : digits;
    }
}
