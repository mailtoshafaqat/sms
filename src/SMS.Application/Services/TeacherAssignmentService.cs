using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;

namespace SMS.Application.Services;

public class TeacherAssignmentService(
    ISchoolRepository schoolRepository,
    IClassRepository classRepository,
    IUnitOfWork unitOfWork) : ITeacherAssignmentService
{
    public async Task<IReadOnlyList<TeacherStaffDto>> GetTeachersAsync(CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var teachers = await classRepository.GetTeachersAsync(school.Id, cancellationToken);
        var sections = await classRepository.GetSectionsWithDetailsAsync(cancellationToken);

        return teachers.Select(teacher =>
        {
            var assigned = sections
                .Where(x => x.ClassTeacherId == teacher.Id)
                .Select(x => $"{x.ClassRoom.Name}-{x.Name}")
                .OrderBy(x => x)
                .ToList();

            return new TeacherStaffDto(
                teacher.Id,
                teacher.EmployeeCode,
                teacher.FullName,
                teacher.UserId,
                null,
                teacher.IsActive,
                assigned);
        }).ToList();
    }

    public async Task<IReadOnlyList<SectionTeacherAssignmentDto>> GetSectionAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        var sections = await classRepository.GetSectionsWithDetailsAsync(cancellationToken);
        var teachers = await classRepository.GetTeachersAsync(
            (await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("School not found.")).Id,
            cancellationToken);
        var teacherLookup = teachers.ToDictionary(x => x.Id);

        return sections
            .Select(section =>
            {
                Teacher? teacher = section.ClassTeacherId is int teacherId && teacherLookup.TryGetValue(teacherId, out var t)
                    ? t
                    : null;

                return new SectionTeacherAssignmentDto(
                    section.Id,
                    section.Name,
                    section.ClassRoom.Name,
                    teacher?.Id,
                    teacher?.FullName);
            })
            .ToList();
    }

    public async Task<int> EnsureTeacherProfileAsync(
        string userId,
        string firstName,
        string lastName,
        string? employeeCode = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await classRepository.GetTeacherByUserIdAsync(userId, tracking: true, cancellationToken);
        if (existing is not null)
        {
            existing.FirstName = firstName.Trim();
            existing.LastName = lastName.Trim();
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var teachers = await classRepository.GetTeachersAsync(school.Id, cancellationToken);
        var code = string.IsNullOrWhiteSpace(employeeCode)
            ? $"T{teachers.Count + 1:000}"
            : employeeCode.Trim();

        var teacher = new Teacher
        {
            SchoolId = school.Id,
            UserId = userId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            EmployeeCode = code,
            IsActive = true
        };
        classRepository.AddTeacher(teacher);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return teacher.Id;
    }

    public async Task AssignSectionTeacherAsync(int sectionId, int? teacherId, CancellationToken cancellationToken = default)
    {
        var section = await classRepository.GetSectionWithClassRoomAsync(sectionId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Section not found.");

        if (teacherId is > 0)
        {
            var teachers = await classRepository.GetTeachersAsync(section.ClassRoom.SchoolId, cancellationToken);
            if (!teachers.Any(x => x.Id == teacherId))
            {
                throw new InvalidOperationException("Teacher not found.");
            }
        }

        section.ClassTeacherId = teacherId;
        section.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
