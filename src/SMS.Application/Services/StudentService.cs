using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Application.Services;

public class StudentService(
    ISchoolRepository schoolRepository,
    IAcademicYearRepository academicYearRepository,
    IStudentRepository studentRepository,
    IBiometricDeviceRepository biometricDeviceRepository,
    IUserAccessService userAccessService,
    IFileStorageService fileStorageService,
    IUnitOfWork unitOfWork) : IStudentService
{
    public async Task<PagedResultDto<StudentListItemDto>> GetStudentsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 25,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        IReadOnlyList<int>? sectionFilter = null;
        if (!string.IsNullOrWhiteSpace(userId) && !await userAccessService.HasFullAttendanceAccessAsync(userId, cancellationToken))
        {
            sectionFilter = await userAccessService.GetAllowedSectionIdsAsync(userId, cancellationToken);
            if (sectionFilter.Count == 0)
            {
                return new PagedResultDto<StudentListItemDto>([], 0, page, pageSize);
            }
        }

        var (enrollments, totalCount) = await studentRepository.GetActiveEnrollmentsPagedAsync(
            currentYear.Id, search, skip, pageSize, sectionFilter, cancellationToken);

        var items = enrollments
            .Select(x => new StudentListItemDto(
                x.Student.Id,
                x.Student.StudentCode,
                x.Student.FirstName + " " + x.Student.LastName,
                x.RollNumber,
                x.Section.ClassRoom.Name,
                x.Section.Name,
                x.Student.Phone,
                x.Student.IsActive,
                x.Student.PhotoPath))
            .ToList();

        return new PagedResultDto<StudentListItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<StudentFormDto?> GetStudentAsync(int id, CancellationToken cancellationToken = default)
    {
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var enrollment = await studentRepository.GetEnrollmentAsync(id, currentYear.Id, cancellationToken: cancellationToken);

        if (enrollment is null)
        {
            return null;
        }

        var fingerprintUserId = await studentRepository.GetBiometricUserIdAsync(id, BiometricType.Fingerprint, cancellationToken);
        var faceUserId = await studentRepository.GetBiometricUserIdAsync(id, BiometricType.Face, cancellationToken);

        return new StudentFormDto
        {
            Id = enrollment.StudentId,
            StudentCode = enrollment.Student.StudentCode,
            FirstName = enrollment.Student.FirstName,
            LastName = enrollment.Student.LastName,
            FatherName = enrollment.Student.FatherName,
            Phone = enrollment.Student.Phone,
            WhatsAppNumber = enrollment.Student.WhatsAppNumber,
            SectionId = enrollment.SectionId,
            RollNumber = enrollment.RollNumber,
            FingerprintUserId = fingerprintUserId,
            FaceUserId = faceUserId,
            IsActive = enrollment.Student.IsActive,
            PhotoPath = enrollment.Student.PhotoPath
        };
    }

    public async Task<int> SaveStudentAsync(StudentFormDto dto, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);

        await ValidateStudentAsync(dto, school.Id, currentYear.Id, cancellationToken);

        Student student;
        StudentEnrollment enrollment;

        if (dto.Id > 0)
        {
            student = await studentRepository.GetStudentByIdAsync(dto.Id, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Student not found.");
            enrollment = await studentRepository.GetEnrollmentAsync(dto.Id, currentYear.Id, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Enrollment not found.");

            student.UpdatedAt = DateTime.UtcNow;
            enrollment.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            student = new Student { SchoolId = school.Id };
            enrollment = new StudentEnrollment
            {
                AcademicYearId = currentYear.Id,
                IsActive = true
            };
            student.Enrollments.Add(enrollment);
            studentRepository.AddStudent(student);
        }

        student.StudentCode = dto.StudentCode.Trim();
        student.FirstName = dto.FirstName.Trim();
        student.LastName = dto.LastName.Trim();
        student.FatherName = dto.FatherName?.Trim();
        student.Phone = dto.Phone?.Trim();
        student.WhatsAppNumber = dto.WhatsAppNumber?.Trim();
        student.IsActive = dto.IsActive;

        enrollment.SectionId = dto.SectionId;
        enrollment.RollNumber = dto.RollNumber.Trim();
        enrollment.IsActive = dto.IsActive;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var fingerprintUserId = dto.FingerprintUserId;
        if (string.IsNullOrWhiteSpace(fingerprintUserId) && !string.IsNullOrWhiteSpace(dto.BiometricUserId))
        {
            fingerprintUserId = dto.BiometricUserId;
        }

        await SaveBiometricMapForTypeAsync(student.Id, fingerprintUserId, BiometricType.Fingerprint, cancellationToken);
        await SaveBiometricMapForTypeAsync(student.Id, dto.FaceUserId, BiometricType.Face, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return student.Id;
    }

    public async Task DeleteStudentAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetStudentByIdAsync(id, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Student not found.");

        student.IsActive = false;
        student.UpdatedAt = DateTime.UtcNow;

        var enrollments = await studentRepository.GetEnrollmentsByStudentIdAsync(id, tracking: true, cancellationToken);
        foreach (var enrollment in enrollments)
        {
            enrollment.IsActive = false;
            enrollment.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> UploadPhotoAsync(int studentId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetStudentByIdAsync(studentId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Student not found.");

        if (!string.IsNullOrWhiteSpace(student.PhotoPath))
        {
            await fileStorageService.DeleteStudentPhotoAsync(student.PhotoPath, cancellationToken);
        }

        student.PhotoPath = await fileStorageService.SaveStudentPhotoAsync(studentId, fileStream, fileName, cancellationToken);
        student.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return student.PhotoPath;
    }

    public async Task RemovePhotoAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetStudentByIdAsync(studentId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Student not found.");

        if (string.IsNullOrWhiteSpace(student.PhotoPath))
        {
            return;
        }

        await fileStorageService.DeleteStudentPhotoAsync(student.PhotoPath, cancellationToken);
        student.PhotoPath = null;
        student.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveBiometricMapForTypeAsync(
        int studentId,
        string? userId,
        BiometricType type,
        CancellationToken cancellationToken)
    {
        var device = await biometricDeviceRepository.GetEnabledDeviceForTypeAsync(type, cancellationToken: cancellationToken);
        if (device is null)
        {
            return;
        }

        var map = await studentRepository.GetBiometricMapAsync(studentId, device.Id, tracking: true, cancellationToken);

        if (string.IsNullOrWhiteSpace(userId))
        {
            if (map is not null && map.BiometricType == type)
            {
                studentRepository.RemoveBiometricMap(map);
            }

            return;
        }

        if (map is null)
        {
            map = new StudentBiometricMap
            {
                StudentId = studentId,
                BiometricDeviceId = device.Id,
                BiometricType = type
            };
            studentRepository.AddBiometricMap(map);
        }
        else
        {
            map.BiometricType = type;
        }

        map.BiometricUserId = userId.Trim();
        map.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ValidateStudentAsync(StudentFormDto dto, int schoolId, int academicYearId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.RollNumber))
        {
            throw new InvalidOperationException("Roll number is required.");
        }

        if (dto.SectionId <= 0)
        {
            throw new InvalidOperationException("Class / section is required.");
        }

        var roll = dto.RollNumber.Trim();
        if (await studentRepository.RollNumberExistsInSectionAsync(academicYearId, dto.SectionId, roll, dto.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Roll number {roll} is already used in this section.");
        }

        var code = dto.StudentCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            code = $"STU-{dto.SectionId}-{roll}";
            dto.StudentCode = code;
        }

        if (await studentRepository.StudentCodeExistsAsync(schoolId, code, dto.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Student code \"{code}\" is already in use. Enter a different code.");
        }

        var fingerprintUserId = dto.FingerprintUserId;
        if (string.IsNullOrWhiteSpace(fingerprintUserId) && !string.IsNullOrWhiteSpace(dto.BiometricUserId))
        {
            fingerprintUserId = dto.BiometricUserId;
        }

        await ValidateBiometricIdAsync(fingerprintUserId, BiometricType.Fingerprint, dto.Id, cancellationToken);
        await ValidateBiometricIdAsync(dto.FaceUserId, BiometricType.Face, dto.Id, cancellationToken);
    }

    private async Task ValidateBiometricIdAsync(
        string? userId,
        BiometricType type,
        int studentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var device = await biometricDeviceRepository.GetEnabledDeviceForTypeAsync(type, cancellationToken: cancellationToken);
        if (device is null)
        {
            return;
        }

        if (await studentRepository.BiometricUserIdExistsOnDeviceAsync(device.Id, userId.Trim(), studentId, cancellationToken))
        {
            var label = type == BiometricType.Fingerprint ? "Fingerprint" : "Face";
            throw new InvalidOperationException($"{label} user ID \"{userId.Trim()}\" is already assigned to another student on the gate device.");
        }
    }
}

