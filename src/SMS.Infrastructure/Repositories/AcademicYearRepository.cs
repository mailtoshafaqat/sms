using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class AcademicYearRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IAcademicYearRepository
{
    public Task<AcademicYear> GetCurrentAsync(bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.AcademicYears.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstAsync(x => x.IsCurrent, cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<AcademicYear>> GetAllAsync(int schoolId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<AcademicYear>)await db.AcademicYears.AsNoTracking()
                .Where(x => x.SchoolId == schoolId)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<AcademicYear?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.AcademicYears.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public Task<bool> CanDeleteAsync(int yearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var year = await db.AcademicYears.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == yearId, cancellationToken);
                if (year is null || year.IsCurrent)
                {
                    return false;
                }

                if (await db.StudentEnrollments.AnyAsync(x => x.AcademicYearId == yearId, cancellationToken))
                {
                    return false;
                }

                if (await db.StudentPromotions.AnyAsync(x => x.AcademicYearId == yearId, cancellationToken))
                {
                    return false;
                }

                if (await db.DailyAttendances.AnyAsync(x => x.AcademicYearId == yearId, cancellationToken))
                {
                    return false;
                }

                return true;
            },
            cancellationToken);

    public void Add(AcademicYear academicYear) => scopedDb.Context.AcademicYears.Add(academicYear);

    public void Remove(AcademicYear academicYear) => scopedDb.Context.AcademicYears.Remove(academicYear);
}
