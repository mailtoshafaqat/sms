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

    public async Task<StaffMemberFormDto?> GetStaffMemberAsync(int teacherId, CancellationToken cancellationToken = default)
    {
        var teacher = await classRepository.GetTeacherByIdAsync(teacherId, cancellationToken: cancellationToken);
        return teacher is null ? null : MapStaffMember(teacher);
    }

    public async Task<int> SaveStaffMemberAsync(StaffMemberFormDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
        {
            throw new InvalidOperationException("First name and last name are required.");
        }

        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        Teacher teacher;
        if (dto.Id > 0)
        {
            teacher = await classRepository.GetTeacherByIdAsync(dto.Id, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Staff member not found.");
        }
        else
        {
            var teachers = await classRepository.GetTeachersAsync(school.Id, cancellationToken);
            teacher = new Teacher
            {
                SchoolId = school.Id,
                EmployeeCode = string.IsNullOrWhiteSpace(dto.EmployeeCode)
                    ? $"S{teachers.Count + 1:000}"
                    : dto.EmployeeCode.Trim(),
                IsActive = true
            };
            classRepository.AddTeacher(teacher);
        }

        var employeeCode = string.IsNullOrWhiteSpace(dto.EmployeeCode)
            ? teacher.EmployeeCode
            : dto.EmployeeCode.Trim();

        if (await classRepository.EmployeeCodeExistsAsync(school.Id, employeeCode, teacher.Id > 0 ? teacher.Id : null, cancellationToken))
        {
            throw new InvalidOperationException($"Employee code '{employeeCode}' is already in use.");
        }

        teacher.FirstName = dto.FirstName.Trim();
        teacher.LastName = dto.LastName.Trim();
        teacher.EmployeeCode = employeeCode;
        teacher.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        teacher.FingerprintUserId = string.IsNullOrWhiteSpace(dto.FingerprintUserId) ? null : dto.FingerprintUserId.Trim();
        teacher.FaceUserId = string.IsNullOrWhiteSpace(dto.FaceUserId) ? null : dto.FaceUserId.Trim();
        teacher.IsActive = dto.IsActive;
        teacher.UpdatedAt = DateTime.UtcNow;

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

    private static StaffMemberFormDto MapStaffMember(Teacher teacher) => new()
    {
        Id = teacher.Id,
        FirstName = teacher.FirstName,
        LastName = teacher.LastName,
        EmployeeCode = teacher.EmployeeCode,
        Phone = teacher.Phone,
        FingerprintUserId = teacher.FingerprintUserId,
        FaceUserId = teacher.FaceUserId,
        IsActive = teacher.IsActive,
        LinkedUserId = teacher.UserId
    };
}
