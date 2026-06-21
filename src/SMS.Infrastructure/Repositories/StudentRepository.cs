using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class StudentRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IStudentRepository
{
    public Task<IReadOnlyList<StudentEnrollment>> GetActiveEnrollmentsAsync(int academicYearId, string? search, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = BuildActiveEnrollmentQuery(db, academicYearId, search);
                return (IReadOnlyList<StudentEnrollment>)await query
                    .OrderBy(x => x.Section.ClassRoom.Name)
                    .ThenBy(x => x.Section.Name)
                    .ThenBy(x => x.RollNumber)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<(IReadOnlyList<StudentEnrollment> Items, int TotalCount)> GetActiveEnrollmentsPagedAsync(
        int academicYearId,
        string? search,
        int skip,
        int take,
        IReadOnlyList<int>? sectionIds = null,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = BuildActiveEnrollmentQuery(db, academicYearId, search, sectionIds);
                var totalCount = await query.CountAsync(cancellationToken);
                var items = await query
                    .OrderBy(x => x.Section.ClassRoom.Name)
                    .ThenBy(x => x.Section.Name)
                    .ThenBy(x => x.RollNumber)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken);

                return ((IReadOnlyList<StudentEnrollment>)items, totalCount);
            },
            cancellationToken);

    private static IQueryable<StudentEnrollment> BuildActiveEnrollmentQuery(
        AppDbContext db,
        int academicYearId,
        string? search,
        IReadOnlyList<int>? sectionIds = null)
    {
        var query = db.StudentEnrollments.AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Section)
            .ThenInclude(x => x.ClassRoom)
            .Where(x => x.AcademicYearId == academicYearId && x.IsActive);

        if (sectionIds is { Count: > 0 })
        {
            query = query.Where(x => sectionIds.Contains(x.SectionId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Student.FirstName.Contains(term) ||
                x.Student.LastName.Contains(term) ||
                x.Student.StudentCode.Contains(term) ||
                x.RollNumber.Contains(term));
        }

        return query;
    }

    public Task<StudentEnrollment?> GetEnrollmentAsync(int studentId, int academicYearId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.StudentEnrollments
                    .Include(x => x.Student)
                    .Include(x => x.Section)
                    .ThenInclude(x => x.ClassRoom)
                    .AsQueryable();

                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicYearId == academicYearId, cancellationToken);
            },
            cancellationToken);

    public Task<string?> GetBiometricUserIdAsync(int studentId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentBiometricMaps.AsNoTracking()
                .Where(x => x.StudentId == studentId)
                .Select(x => x.BiometricUserId)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);

    public Task<string?> GetBiometricUserIdAsync(int studentId, BiometricType type, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentBiometricMaps.AsNoTracking()
                .Where(x => x.StudentId == studentId && x.BiometricType == type)
                .Select(x => x.BiometricUserId)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);

    public Task<Student?> GetStudentByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Students.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<StudentEnrollment>> GetEnrollmentsByStudentIdAsync(int studentId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.StudentEnrollments.Where(x => x.StudentId == studentId);
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return (IReadOnlyList<StudentEnrollment>)await query.ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<StudentEnrollment>> GetEnrollmentsForStudentsAsync(
        IReadOnlyCollection<int> studentIds,
        int academicYearId,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                if (studentIds.Count == 0)
                {
                    return (IReadOnlyList<StudentEnrollment>)[];
                }

                return (IReadOnlyList<StudentEnrollment>)await db.StudentEnrollments.AsNoTracking()
                    .Include(x => x.Section)
                    .ThenInclude(x => x.ClassRoom)
                    .Where(x => studentIds.Contains(x.StudentId) && x.AcademicYearId == academicYearId && x.IsActive)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<bool> RollNumberExistsInSectionAsync(
        int academicYearId,
        int sectionId,
        string rollNumber,
        int excludeStudentId,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentEnrollments.AsNoTracking()
                .AnyAsync(
                    x => x.AcademicYearId == academicYearId
                         && x.SectionId == sectionId
                         && x.RollNumber == rollNumber
                         && x.StudentId != excludeStudentId
                         && x.IsActive,
                    cancellationToken),
            cancellationToken);

    public Task<bool> StudentCodeExistsAsync(
        int schoolId,
        string studentCode,
        int excludeStudentId,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.Students.AsNoTracking()
                .AnyAsync(
                    x => x.SchoolId == schoolId
                         && x.StudentCode == studentCode
                         && x.Id != excludeStudentId,
                    cancellationToken),
            cancellationToken);

    public Task<bool> BiometricUserIdExistsOnDeviceAsync(
        int deviceId,
        string biometricUserId,
        int excludeStudentId,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentBiometricMaps.AsNoTracking()
                .AnyAsync(
                    x => x.BiometricDeviceId == deviceId
                         && x.BiometricUserId == biometricUserId
                         && x.StudentId != excludeStudentId,
                    cancellationToken),
            cancellationToken);

    public void AddStudent(Student student) => scopedDb.Context.Students.Add(student);

    public Task<StudentBiometricMap?> GetBiometricMapAsync(int studentId, int deviceId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.StudentBiometricMaps.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.StudentId == studentId && x.BiometricDeviceId == deviceId, cancellationToken);
            },
            cancellationToken);

    public void AddBiometricMap(StudentBiometricMap map) => scopedDb.Context.StudentBiometricMaps.Add(map);

    public void RemoveBiometricMap(StudentBiometricMap map) => scopedDb.Context.StudentBiometricMaps.Remove(map);
}
