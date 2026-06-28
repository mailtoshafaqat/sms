using SMS.Domain.Entities.Shared;

namespace SMS.Application.Interfaces.Repositories;

public interface IClassRepository
{
    Task<IReadOnlyList<ClassRoom>> GetClassesAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ClassRoom> Items, int TotalCount)> GetClassesPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<ClassRoom?> GetClassByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<int> GetClassCountAsync(CancellationToken cancellationToken = default);
    void AddClass(ClassRoom classRoom);
    Task<IReadOnlyList<Section>> GetActiveSectionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Section>> GetSectionsWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Section> Items, int TotalCount)> GetSectionsPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Section>> GetSectionsByClassIdAsync(int classRoomId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetSectionIdsForTeacherUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Teacher>> GetTeachersAsync(int schoolId, CancellationToken cancellationToken = default);
    Task<Teacher?> GetTeacherByUserIdAsync(string userId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<Teacher?> GetTeacherByIdAsync(int teacherId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<bool> EmployeeCodeExistsAsync(int schoolId, string employeeCode, int? excludeTeacherId = null, CancellationToken cancellationToken = default);
    void AddTeacher(Teacher teacher);
    Task<int> GetMaxSectionDisplayOrderAsync(int classRoomId, CancellationToken cancellationToken = default);
    Task<Section?> GetSectionByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<Section?> GetSectionWithClassRoomAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<Section?> FindNextSectionAsync(int currentSectionId, CancellationToken cancellationToken = default);
    void AddSection(Section section);
    Task<ClassRoom?> GetClassWithSectionsAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<int> GetActiveEnrollmentCountBySectionAsync(int sectionId, int academicYearId, CancellationToken cancellationToken = default);
    Task<int> GetActiveEnrollmentCountByClassAsync(int classRoomId, int academicYearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetActiveEnrollmentCountsBySectionAsync(int academicYearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetActiveEnrollmentCountsByClassAsync(int academicYearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetSectionIdsByClassOrderedAsync(CancellationToken cancellationToken = default);
    Task<bool> SectionHasHistoryAsync(int sectionId, CancellationToken cancellationToken = default);
    void RemoveSection(Section section);
    void RemoveClass(ClassRoom classRoom);
}

