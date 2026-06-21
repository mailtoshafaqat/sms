using System.Text;
using SMS.Application.Common;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
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
    public async Task QueueAbsentNotificationsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        if (!school.NotifyAbsent)
        {
            return;
        }

        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var records = (await attendanceRepository.GetDailyRecordsAsync(date, cancellationToken))
            .Where(x => x.Status == AttendanceStatus.Absent)
            .ToList();

        foreach (var record in records)
        {
            if (await notificationRepository.ExistsAsync(record.StudentId, date, AttendanceNotificationType.Absent, cancellationToken))
            {
                continue;
            }

            var enrollment = await studentRepository.GetEnrollmentAsync(record.StudentId, currentYear.Id, cancellationToken: cancellationToken);
            if (enrollment is null)
            {
                continue;
            }

            var phone = ResolvePhone(enrollment.Student.WhatsAppNumber, enrollment.Student.Phone);
            if (string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            var message = NotificationMessageBuilder.BuildAbsentMessage(
                school.AbsentNotificationTemplate,
                school.Name,
                enrollment.Student.FullName,
                date,
                enrollment.Section.ClassRoom.Name,
                enrollment.Section.Name);
            notificationRepository.Add(new AttendanceNotificationLog
            {
                SchoolId = school.Id,
                StudentId = record.StudentId,
                AttendanceDate = date,
                NotificationType = AttendanceNotificationType.Absent,
                RecipientPhone = phone,
                Message = message
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task QueueLateNotificationAsync(int studentId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        if (!school.NotifyLate)
        {
            return;
        }

        if (await notificationRepository.ExistsAsync(studentId, date, AttendanceNotificationType.Late, cancellationToken))
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

        var message = NotificationMessageBuilder.BuildLateMessage(
            school.LateNotificationTemplate,
            school.Name,
            enrollment.Student.FullName,
            date);
        notificationRepository.Add(new AttendanceNotificationLog
        {
            SchoolId = school.Id,
            StudentId = studentId,
            AttendanceDate = date,
            NotificationType = AttendanceNotificationType.Late,
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
                log.NotificationType.ToString(),
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
