using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;

namespace SMS.Application.Services;

public class AcademicYearService(
    ISchoolRepository schoolRepository,
    IAcademicYearRepository academicYearRepository,
    IStudentRepository studentRepository,
    IUnitOfWork unitOfWork) : IAcademicYearService
{
    public async Task<IReadOnlyList<AcademicYearDto>> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var years = await academicYearRepository.GetAllAsync(school.Id, cancellationToken);
        var results = new List<AcademicYearDto>();

        foreach (var year in years)
        {
            var enrollments = await studentRepository.GetActiveEnrollmentsAsync(year.Id, null, cancellationToken);
            var canDelete = !year.IsCurrent && await academicYearRepository.CanDeleteAsync(year.Id, cancellationToken);
            results.Add(new AcademicYearDto(
                year.Id,
                year.Name,
                year.StartDate,
                year.EndDate,
                year.IsCurrent,
                enrollments.Count,
                canDelete));
        }

        return results;
    }

    public async Task<int> SaveYearAsync(AcademicYearFormDto dto, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("End date must be on or after start date.");
        }

        AcademicYear entity;
        if (dto.Id > 0)
        {
            entity = await academicYearRepository.GetByIdAsync(dto.Id, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Academic year not found.");
            entity.Name = dto.Name.Trim();
            entity.StartDate = dto.StartDate;
            entity.EndDate = dto.EndDate;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new AcademicYear
            {
                SchoolId = school.Id,
                Name = dto.Name.Trim(),
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsCurrent = false
            };
            academicYearRepository.Add(entity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task SetCurrentYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");
        var target = await academicYearRepository.GetByIdAsync(yearId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Academic year not found.");

        var allYears = await academicYearRepository.GetAllAsync(school.Id, cancellationToken);
        foreach (var year in allYears)
        {
            var tracked = await academicYearRepository.GetByIdAsync(year.Id, tracking: true, cancellationToken);
            if (tracked is null)
            {
                continue;
            }

            tracked.IsCurrent = tracked.Id == yearId;
            tracked.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var year = await academicYearRepository.GetByIdAsync(yearId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Academic year not found.");

        if (year.IsCurrent)
        {
            throw new InvalidOperationException("Cannot delete the current academic year.");
        }

        if (!await academicYearRepository.CanDeleteAsync(yearId, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete a year that has students, promotions, or attendance records.");
        }

        academicYearRepository.Remove(year);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AcademicYearSessionAlertDto?> GetSessionEndAlertAsync(CancellationToken cancellationToken = default)
    {
        AcademicYear current;
        try
        {
            current = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysRemaining = current.EndDate.DayNumber - today.DayNumber;
        const int warningThresholdDays = 60;
        if (daysRemaining > warningThresholdDays)
        {
            return null;
        }

        var isPastEndDate = daysRemaining < 0;
        var severity = isPastEndDate || daysRemaining <= 30 ? "critical" : "warning";
        var message = isPastEndDate
            ? $"Session \"{current.Name}\" ended {Math.Abs(daysRemaining)} day(s) ago. Promote students and set a new current year."
            : daysRemaining == 0
                ? $"Session \"{current.Name}\" ends today. Plan promotion and the next academic year."
                : $"Session \"{current.Name}\" ends in {daysRemaining} day(s) on {current.EndDate:d}.";

        return new AcademicYearSessionAlertDto
        {
            AcademicYearId = current.Id,
            YearName = current.Name,
            EndDate = current.EndDate,
            DaysRemaining = daysRemaining,
            IsPastEndDate = isPastEndDate,
            Severity = severity,
            Message = message
        };
    }
}
