using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces.Repositories;

public interface IAttendanceNotificationRepository
{
    void Add(AttendanceNotificationLog log);
    Task<bool> ExistsAsync(int studentId, DateOnly date, AttendanceNotificationType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceNotificationLog>> GetByDateAsync(int schoolId, DateOnly date, CancellationToken cancellationToken = default);
    Task<AttendanceNotificationLog?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
}
