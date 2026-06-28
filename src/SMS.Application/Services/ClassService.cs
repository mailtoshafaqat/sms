using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;

namespace SMS.Application.Services;

public class ClassService(
    ISchoolRepository schoolRepository,
    IClassRepository classRepository,
    IAcademicYearRepository academicYearRepository,
    IUserAccessService userAccessService,
    IUnitOfWork unitOfWork) : IClassService
{
    public async Task<IReadOnlyList<ClassRoomDto>> GetClassesAsync(CancellationToken cancellationToken = default)
    {
        var academicYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var classes = await classRepository.GetClassesAsync(cancellationToken);
        var metadata = await LoadEnrollmentMetadataAsync(academicYear.Id, cancellationToken);
        return MapClasses(classes, classes, metadata.ClassCounts);
    }

    public async Task<ClassTreeDto> GetClassTreeAsync(CancellationToken cancellationToken = default)
    {
        var academicYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);

        var classes = await classRepository.GetClassesAsync(cancellationToken);
        var sections = await classRepository.GetSectionsWithDetailsAsync(cancellationToken);
        var classCounts = await classRepository.GetActiveEnrollmentCountsByClassAsync(academicYear.Id, cancellationToken);
        var sectionCounts = await classRepository.GetActiveEnrollmentCountsBySectionAsync(academicYear.Id, cancellationToken);
        var sectionOrderByClass = await classRepository.GetSectionIdsByClassOrderedAsync(cancellationToken);

        return new ClassTreeDto(
            MapClasses(classes, classes, classCounts),
            MapSections(sections, sectionCounts, sectionOrderByClass));
    }

    public async Task<PagedResultDto<ClassRoomDto>> GetClassesPagedAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var academicYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var allClasses = await classRepository.GetClassesAsync(cancellationToken);
        var (classes, totalCount) = await classRepository.GetClassesPagedAsync(skip, pageSize, cancellationToken);
        var metadata = await LoadEnrollmentMetadataAsync(academicYear.Id, cancellationToken);
        var items = MapClasses(classes, allClasses, metadata.ClassCounts);

        return new PagedResultDto<ClassRoomDto>(items, totalCount, page, pageSize);
    }

    public async Task<int> SaveClassAsync(string name, int? id = null, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        ClassRoom entity;

        if (id is > 0)
        {
            entity = await classRepository.GetClassByIdAsync(id.Value, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Class not found.");
            entity.Name = name.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new ClassRoom
            {
                SchoolId = school.Id,
                Name = name.Trim(),
                DisplayOrder = await classRepository.GetClassCountAsync(cancellationToken) + 1
            };
            classRepository.AddClass(entity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<int> SaveSectionAsync(int classRoomId, string sectionName, int? sectionId = null, CancellationToken cancellationToken = default)
    {
        Section entity;

        if (sectionId is > 0)
        {
            entity = await classRepository.GetSectionByIdAsync(sectionId.Value, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Section not found.");
            entity.Name = sectionName.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await classRepository.GetClassByIdAsync(classRoomId, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Class not found.");

            entity = new Section
            {
                ClassRoomId = classRoomId,
                Name = sectionName.Trim(),
                DisplayOrder = await classRepository.GetMaxSectionDisplayOrderAsync(classRoomId, cancellationToken) + 1
            };
            classRepository.AddSection(entity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task MoveClassAsync(int classId, bool moveUp, CancellationToken cancellationToken = default)
    {
        var ordered = await classRepository.GetClassesAsync(cancellationToken);
        var index = ordered.ToList().FindIndex(x => x.Id == classId);
        if (index < 0)
        {
            throw new InvalidOperationException("Class not found.");
        }

        var targetIndex = moveUp ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= ordered.Count)
        {
            return;
        }

        var current = await classRepository.GetClassByIdAsync(classId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Class not found.");
        var neighbor = await classRepository.GetClassByIdAsync(ordered[targetIndex].Id, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Class not found.");

        (current.DisplayOrder, neighbor.DisplayOrder) = (neighbor.DisplayOrder, current.DisplayOrder);
        current.UpdatedAt = DateTime.UtcNow;
        neighbor.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveSectionAsync(int sectionId, bool moveUp, CancellationToken cancellationToken = default)
    {
        var section = await classRepository.GetSectionByIdAsync(sectionId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Section not found.");

        var siblings = await classRepository.GetSectionsByClassIdAsync(section.ClassRoomId, tracking: true, cancellationToken);
        var index = siblings.ToList().FindIndex(x => x.Id == sectionId);
        if (index < 0)
        {
            throw new InvalidOperationException("Section not found.");
        }

        var targetIndex = moveUp ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= siblings.Count)
        {
            return;
        }

        var neighbor = siblings[targetIndex];
        (section.DisplayOrder, neighbor.DisplayOrder) = (neighbor.DisplayOrder, section.DisplayOrder);
        section.UpdatedAt = DateTime.UtcNow;
        neighbor.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetClassActiveAsync(int classId, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await classRepository.GetClassWithSectionsAsync(classId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Class not found.");

        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;

        foreach (var section in entity.Sections)
        {
            section.IsActive = isActive;
            section.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetSectionActiveAsync(int sectionId, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await classRepository.GetSectionByIdAsync(sectionId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Section not found.");

        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSectionAsync(int sectionId, CancellationToken cancellationToken = default)
    {
        _ = await classRepository.GetSectionWithClassRoomAsync(sectionId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Section not found.");

        await EnsureSectionDeletableAsync(sectionId, cancellationToken);

        var section = await classRepository.GetSectionByIdAsync(sectionId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Section not found.");

        classRepository.RemoveSection(section);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteClassAsync(int classId, CancellationToken cancellationToken = default)
    {
        var entity = await classRepository.GetClassWithSectionsAsync(classId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Class not found.");

        foreach (var section in entity.Sections)
        {
            await EnsureSectionDeletableAsync(section.Id, cancellationToken);
        }

        foreach (var section in entity.Sections.ToList())
        {
            classRepository.RemoveSection(section);
        }

        classRepository.RemoveClass(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassSectionOptionDto>> GetSectionOptionsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var sections = await classRepository.GetActiveSectionsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var allowed = await userAccessService.GetAllowedSectionIdsAsync(userId, cancellationToken);
            sections = sections.Where(x => allowed.Contains(x.Id)).ToList();
        }

        return sections
            .Where(x => x.ClassRoom.IsActive)
            .Select(x => new ClassSectionOptionDto(x.Id, x.ClassRoom.Name + "-" + x.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<SectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default)
    {
        var academicYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var sections = await classRepository.GetSectionsWithDetailsAsync(cancellationToken);
        var metadata = await LoadEnrollmentMetadataAsync(academicYear.Id, cancellationToken);
        return MapSections(sections, metadata.SectionCounts, metadata.SectionOrderByClass);
    }

    public async Task<PagedResultDto<SectionDto>> GetSectionsPagedAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var academicYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var (sections, totalCount) = await classRepository.GetSectionsPagedAsync(skip, pageSize, cancellationToken);
        var metadata = await LoadEnrollmentMetadataAsync(academicYear.Id, cancellationToken);
        var items = MapSections(sections, metadata.SectionCounts, metadata.SectionOrderByClass);

        return new PagedResultDto<SectionDto>(items, totalCount, page, pageSize);
    }

    private sealed record EnrollmentMetadata(
        IReadOnlyDictionary<int, int> ClassCounts,
        IReadOnlyDictionary<int, int> SectionCounts,
        IReadOnlyDictionary<int, IReadOnlyList<int>> SectionOrderByClass);

    private async Task<EnrollmentMetadata> LoadEnrollmentMetadataAsync(int academicYearId, CancellationToken cancellationToken)
    {
        var classCounts = await classRepository.GetActiveEnrollmentCountsByClassAsync(academicYearId, cancellationToken);
        var sectionCounts = await classRepository.GetActiveEnrollmentCountsBySectionAsync(academicYearId, cancellationToken);
        var sectionOrder = await classRepository.GetSectionIdsByClassOrderedAsync(cancellationToken);

        return new EnrollmentMetadata(classCounts, sectionCounts, sectionOrder);
    }

    private static IReadOnlyList<ClassRoomDto> MapClasses(
        IReadOnlyList<ClassRoom> classes,
        IReadOnlyList<ClassRoom> orderedClasses,
        IReadOnlyDictionary<int, int> enrollmentCounts)
    {
        var orderLookup = orderedClasses.Select((x, index) => (x.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var results = new List<ClassRoomDto>(classes.Count);

        foreach (var cls in classes)
        {
            var index = orderLookup.GetValueOrDefault(cls.Id, -1);
            results.Add(new ClassRoomDto(
                cls.Id,
                cls.Name,
                cls.Sections.Count,
                enrollmentCounts.GetValueOrDefault(cls.Id, 0),
                cls.IsActive,
                index > 0,
                index >= 0 && index < orderedClasses.Count - 1));
        }

        return results;
    }

    private static IReadOnlyList<SectionDto> MapSections(
        IReadOnlyList<Section> sections,
        IReadOnlyDictionary<int, int> enrollmentCounts,
        IReadOnlyDictionary<int, IReadOnlyList<int>> sectionOrderByClass)
    {
        var sectionIndex = sectionOrderByClass
            .SelectMany(kvp => kvp.Value.Select((id, index) => (id, index)))
            .ToDictionary(x => x.id, x => x.index);

        var results = new List<SectionDto>(sections.Count);
        foreach (var section in sections)
        {
            var siblingCount = sectionOrderByClass.GetValueOrDefault(section.ClassRoomId, []).Count;
            var index = sectionIndex.GetValueOrDefault(section.Id, -1);

            results.Add(new SectionDto(
                section.Id,
                section.ClassRoomId,
                section.ClassRoom.Name,
                section.Name,
                section.ClassTeacher != null ? section.ClassTeacher.FirstName + " " + section.ClassTeacher.LastName : null,
                enrollmentCounts.GetValueOrDefault(section.Id, 0),
                section.IsActive,
                index > 0,
                index >= 0 && index < siblingCount - 1));
        }

        return results;
    }

    private async Task EnsureSectionDeletableAsync(int sectionId, CancellationToken cancellationToken)
    {
        var academicYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var studentCount = await classRepository.GetActiveEnrollmentCountBySectionAsync(
            sectionId,
            academicYear.Id,
            cancellationToken);

        if (studentCount > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete: {studentCount} student(s) are enrolled. Reassign or promote them first, or deactivate the section instead.");
        }

        if (await classRepository.SectionHasHistoryAsync(sectionId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Cannot delete: this section has enrollment, attendance, or promotion history. Deactivate it instead.");
        }
    }
}
