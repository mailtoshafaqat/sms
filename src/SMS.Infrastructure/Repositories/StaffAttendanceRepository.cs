using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class StaffAttendanceRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IStaffAttendanceRepository
{
    public Task<IReadOnlyList<StaffDailyAttendance>> GetByDateAsync(int schoolId, DateOnly date, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<StaffDailyAttendance>)await db.StaffDailyAttendances.AsNoTracking()
                .Include(x => x.Teacher)
                .Where(x => x.SchoolId == schoolId && x.AttendanceDate == date)
                .OrderBy(x => x.Teacher.FirstName)
                .ThenBy(x => x.Teacher.LastName)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<StaffDailyAttendance?> GetByTeacherAndDateAsync(int teacherId, DateOnly date, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.StaffDailyAttendances.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.TeacherId == teacherId && x.AttendanceDate == date, cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<StaffDailyAttendance>> GetByDateRangeAsync(
        int schoolId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<StaffDailyAttendance>)await db.StaffDailyAttendances.AsNoTracking()
                .Include(x => x.Teacher)
                .Where(x => x.SchoolId == schoolId && x.AttendanceDate >= startDate && x.AttendanceDate <= endDate)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public void Add(StaffDailyAttendance record) => scopedDb.Context.StaffDailyAttendances.Add(record);
}
