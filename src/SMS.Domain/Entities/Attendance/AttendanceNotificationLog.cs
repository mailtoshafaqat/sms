using SMS.Domain.Common;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Domain.Entities.Attendance;

public class AttendanceNotificationLog : BaseEntity
{
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public AttendanceNotificationType NotificationType { get; set; }
    public string RecipientPhone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }

    public Student Student { get; set; } = null!;
}
