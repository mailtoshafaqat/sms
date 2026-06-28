using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class ClassRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IClassRepository
{
    public Task<IReadOnlyList<ClassRoom>> GetClassesAsync(CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<ClassRoom>)await db.ClassRooms.AsNoTracking()
                .Include(x => x.Sections)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<(IReadOnlyList<ClassRoom> Items, int TotalCount)> GetClassesPagedAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = db.ClassRooms.AsNoTracking()
                    .Include(x => x.Sections)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name);

                var totalCount = await query.CountAsync(cancellationToken);
                var items = await query
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken);

                return ((IReadOnlyList<ClassRoom>)items, totalCount);
            },
            cancellationToken);

    public Task<ClassRoom?> GetClassByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.ClassRooms.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public Task<int> GetClassCountAsync(CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.ClassRooms.CountAsync(cancellationToken),
            cancellationToken);

    public void AddClass(ClassRoom classRoom) => scopedDb.Context.ClassRooms.Add(classRoom);

    public Task<IReadOnlyList<Section>> GetActiveSectionsAsync(CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<Section>)await db.Sections.AsNoTracking()
                .Include(x => x.ClassRoom)
                .Where(x => x.IsActive)
                .OrderBy(x => x.ClassRoom.DisplayOrder)
                .ThenBy(x => x.ClassRoom.Name)
                .ThenBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<Section>> GetSectionsWithDetailsAsync(CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<Section>)await db.Sections.AsNoTracking()
                .Include(x => x.ClassRoom)
                .Include(x => x.ClassTeacher)
                .OrderBy(x => x.ClassRoom.DisplayOrder)
                .ThenBy(x => x.ClassRoom.Name)
                .ThenBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<(IReadOnlyList<Section> Items, int TotalCount)> GetSectionsPagedAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = db.Sections.AsNoTracking()
                    .Include(x => x.ClassRoom)
                    .Include(x => x.ClassTeacher)
                    .OrderBy(x => x.ClassRoom.DisplayOrder)
                    .ThenBy(x => x.ClassRoom.Name)
                    .ThenBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name);

                var totalCount = await query.CountAsync(cancellationToken);
                var items = await query
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken);

                return ((IReadOnlyList<Section>)items, totalCount);
            },
            cancellationToken);

    public Task<Section?> GetSectionByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Sections.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public void AddSection(Section section) => scopedDb.Context.Sections.Add(section);

    public Task<IReadOnlyList<Section>> GetSectionsByClassIdAsync(int classRoomId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Sections
                    .Where(x => x.ClassRoomId == classRoomId)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name)
                    .AsQueryable();

                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return (IReadOnlyList<Section>)await query.ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<int> GetMaxSectionDisplayOrderAsync(int classRoomId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var max = await db.Sections
                    .Where(x => x.ClassRoomId == classRoomId)
                    .Select(x => (int?)x.DisplayOrder)
                    .MaxAsync(cancellationToken);

                return max ?? 0;
            },
            cancellationToken);

    public Task<Section?> GetSectionWithClassRoomAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Sections
                    .Include(x => x.ClassRoom)
                    .AsQueryable();

                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public Task<ClassRoom?> GetClassWithSectionsAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.ClassRooms
                    .Include(x => x.Sections)
                    .AsQueryable();

                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public Task<int> GetActiveEnrollmentCountBySectionAsync(int sectionId, int academicYearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentEnrollments.CountAsync(
                x => x.SectionId == sectionId
                     && x.AcademicYearId == academicYearId
                     && x.IsActive
                     && x.Student.IsActive,
                cancellationToken),
            cancellationToken);

    public Task<int> GetActiveEnrollmentCountByClassAsync(int classRoomId, int academicYearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            db => db.StudentEnrollments.CountAsync(
                x => x.Section.ClassRoomId == classRoomId
                     && x.AcademicYearId == academicYearId
                     && x.IsActive
                     && x.Student.IsActive,
                cancellationToken),
            cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetActiveEnrollmentCountsBySectionAsync(int academicYearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyDictionary<int, int>)await db.StudentEnrollments.AsNoTracking()
                .Where(x => x.AcademicYearId == academicYearId && x.IsActive && x.Student.IsActive)
                .GroupBy(x => x.SectionId)
                .Select(g => new { SectionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectionId, x => x.Count, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetActiveEnrollmentCountsByClassAsync(int academicYearId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyDictionary<int, int>)await db.StudentEnrollments.AsNoTracking()
                .Where(x => x.AcademicYearId == academicYearId && x.IsActive && x.Student.IsActive)
                .GroupBy(x => x.Section.ClassRoomId)
                .Select(g => new { ClassRoomId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassRoomId, x => x.Count, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetSectionIdsByClassOrderedAsync(CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var rows = await db.Sections.AsNoTracking()
                    .OrderBy(x => x.ClassRoomId)
                    .ThenBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name)
                    .Select(x => new { x.Id, x.ClassRoomId })
                    .ToListAsync(cancellationToken);

                return (IReadOnlyDictionary<int, IReadOnlyList<int>>)rows
                    .GroupBy(x => x.ClassRoomId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.Id).ToList());
            },
            cancellationToken);

    public Task<bool> SectionHasHistoryAsync(int sectionId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                if (await db.StudentEnrollments.AnyAsync(x => x.SectionId == sectionId, cancellationToken))
                {
                    return true;
                }

                if (await db.DailyAttendances.AnyAsync(x => x.SectionId == sectionId, cancellationToken))
                {
                    return true;
                }

                return await db.StudentPromotions.AnyAsync(
                    x => x.FromSectionId == sectionId || x.ToSectionId == sectionId,
                    cancellationToken);
            },
            cancellationToken);

    public void RemoveSection(Section section) => scopedDb.Context.Sections.Remove(section);

    public void RemoveClass(ClassRoom classRoom) => scopedDb.Context.ClassRooms.Remove(classRoom);

    public Task<Section?> FindNextSectionAsync(int currentSectionId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var current = await db.Sections.AsNoTracking()
                    .Include(x => x.ClassRoom)
                    .FirstOrDefaultAsync(x => x.Id == currentSectionId, cancellationToken);

                if (current is null)
                {
                    return null;
                }

                var nextClass = await db.ClassRooms.AsNoTracking()
                    .Where(x => x.IsActive && x.DisplayOrder > current.ClassRoom.DisplayOrder)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (nextClass is null)
                {
                    return null;
                }

                var matchedSection = await db.Sections.AsNoTracking()
                    .Include(x => x.ClassRoom)
                    .Where(x => x.ClassRoomId == nextClass.Id && x.IsActive && x.Name == current.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (matchedSection is not null)
                {
                    return matchedSection;
                }

                return await db.Sections.AsNoTracking()
                    .Include(x => x.ClassRoom)
                    .Where(x => x.ClassRoomId == nextClass.Id && x.IsActive)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<int>> GetSectionIdsForTeacherUserAsync(string userId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var teacher = await db.Teachers.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
                if (teacher is null)
                {
                    return (IReadOnlyList<int>)[];
                }

                return (IReadOnlyList<int>)await db.Sections.AsNoTracking()
                    .Where(x => x.ClassTeacherId == teacher.Id && x.IsActive)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<Teacher>> GetTeachersAsync(int schoolId, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<Teacher>)await db.Teachers.AsNoTracking()
                .Where(x => x.SchoolId == schoolId)
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<Teacher?> GetTeacherByUserIdAsync(string userId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Teachers.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            },
            cancellationToken);

    public Task<Teacher?> GetTeacherByIdAsync(int teacherId, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.Teachers.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == teacherId, cancellationToken);
            },
            cancellationToken);

    public Task<bool> EmployeeCodeExistsAsync(int schoolId, string employeeCode, int? excludeTeacherId = null, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db =>
            {
                var query = db.Teachers.AsNoTracking()
                    .Where(x => x.SchoolId == schoolId && x.EmployeeCode == employeeCode);
                if (excludeTeacherId is > 0)
                {
                    query = query.Where(x => x.Id != excludeTeacherId);
                }

                return await query.AnyAsync(cancellationToken);
            },
            cancellationToken);

    public void AddTeacher(Teacher teacher) => scopedDb.Context.Teachers.Add(teacher);
}
