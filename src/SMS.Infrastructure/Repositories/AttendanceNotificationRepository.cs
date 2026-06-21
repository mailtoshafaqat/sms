using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class AttendanceNotificationRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IAttendanceNotificationRepository
{
    public void Add(AttendanceNotificationLog log) => scopedDb.Context.Set<AttendanceNotificationLog>().Add(log);

    public Task<bool> ExistsAsync(int studentId, DateOnly date, AttendanceNotificationType type, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.Set<AttendanceNotificationLog>().AsNoTracking()
                .AnyAsync(x => x.StudentId == studentId && x.AttendanceDate == date && x.NotificationType == type, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<AttendanceNotificationLog>> GetByDateAsync(int schoolId, DateOnly date, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<AttendanceNotificationLog>)await db.Set<AttendanceNotificationLog>()
                .AsNoTracking()
                .Include(x => x.Student)
                .Where(x => x.SchoolId == schoolId && x.AttendanceDate == date)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<AttendanceNotificationLog?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Set<AttendanceNotificationLog>().AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);
}
