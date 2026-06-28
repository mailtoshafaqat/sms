using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces.Repositories;

public interface IStaffAttendanceRepository
{
    Task<IReadOnlyList<StaffDailyAttendance>> GetByDateAsync(int schoolId, DateOnly date, CancellationToken cancellationToken = default);
    Task<StaffDailyAttendance?> GetByTeacherAndDateAsync(int teacherId, DateOnly date, bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffDailyAttendance>> GetByDateRangeAsync(int schoolId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    void Add(StaffDailyAttendance record);
}
