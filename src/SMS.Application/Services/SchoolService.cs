using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;

namespace SMS.Application.Services;

public class SchoolService(
    ISchoolRepository schoolRepository,
    IUnitOfWork unitOfWork,
    IFileStorageService fileStorageService) : ISchoolService
{
    public async Task<SchoolSettingsDto?> GetSchoolSettingsAsync(CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken);
        return school is null
            ? null
            : MapToDto(school);
    }

    public async Task UpdateSchoolSettingsAsync(SchoolSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetByIdAsync(dto.Id, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        school.Name = dto.Name.Trim();
        school.Address = dto.Address?.Trim();
        school.Phone = dto.Phone?.Trim();
        school.WhatsAppNumber = dto.WhatsAppNumber?.Trim();
        school.Email = dto.Email?.Trim();
        school.SchoolStartTime = dto.SchoolStartTime;
        school.LateAfterMinutes = dto.LateAfterMinutes;
        school.SchoolEndTime = dto.SchoolEndTime;
        school.WeeklyOffDays = dto.WeeklyOffDays;
        school.NotifyAbsent = dto.NotifyAbsent;
        school.NotifyLate = dto.NotifyLate;
        school.AbsentNotificationTemplate = NormalizeTemplate(dto.AbsentNotificationTemplate);
        school.LateNotificationTemplate = NormalizeTemplate(dto.LateNotificationTemplate);
        school.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> UploadLogoAsync(int schoolId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetByIdAsync(schoolId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        if (!string.IsNullOrWhiteSpace(school.LogoPath))
        {
            await fileStorageService.DeleteSchoolLogoAsync(school.LogoPath, cancellationToken);
        }

        school.LogoPath = await fileStorageService.SaveSchoolLogoAsync(schoolId, fileStream, fileName, cancellationToken);
        school.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return school.LogoPath;
    }

    public async Task RemoveLogoAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetByIdAsync(schoolId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        if (!string.IsNullOrWhiteSpace(school.LogoPath))
        {
            await fileStorageService.DeleteSchoolLogoAsync(school.LogoPath, cancellationToken);
            school.LogoPath = null;
            school.UpdatedAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static SchoolSettingsDto MapToDto(Domain.Entities.Shared.School school) => new()
    {
        Id = school.Id,
        Name = school.Name,
        Address = school.Address,
        Phone = school.Phone,
        WhatsAppNumber = school.WhatsAppNumber,
        Email = school.Email,
        LogoPath = school.LogoPath,
        SchoolStartTime = school.SchoolStartTime,
        LateAfterMinutes = school.LateAfterMinutes,
        SchoolEndTime = school.SchoolEndTime,
        WeeklyOffDays = school.WeeklyOffDays,
        NotifyAbsent = school.NotifyAbsent,
        NotifyLate = school.NotifyLate,
        AbsentNotificationTemplate = school.AbsentNotificationTemplate,
        LateNotificationTemplate = school.LateNotificationTemplate
    };

    private static string? NormalizeTemplate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }
}

