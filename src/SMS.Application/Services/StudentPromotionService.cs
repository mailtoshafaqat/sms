using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Application.Services;

public class StudentPromotionService(
    IAcademicYearRepository academicYearRepository,
    IClassRepository classRepository,
    IAttendanceRepository attendanceRepository,
    IStudentRepository studentRepository,
    IStudentPromotionRepository promotionRepository,
    IUnitOfWork unitOfWork) : IStudentPromotionService
{
    public async Task<IReadOnlyList<StudentPromotionCandidateDto>> GetCandidatesAsync(
        int sectionId,
        CancellationToken cancellationToken = default)
    {
        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var enrollments = await attendanceRepository.GetSectionStudentsAsync(sectionId, currentYear.Id, cancellationToken);
        var nextSection = await classRepository.FindNextSectionAsync(sectionId, cancellationToken);

        return enrollments
            .Select(x => MapCandidate(x, nextSection))
            .OrderBy(x => x.RollNumber)
            .ToList();
    }

    public async Task<PromotionResultDto> PromoteStudentsAsync(
        int sectionId,
        IReadOnlyList<int> studentIds,
        int? targetSectionId,
        PromotionSource source,
        string? promotedByUserId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return new PromotionResultDto(0, 0, ["Select at least one student."]);
        }

        var currentYear = await academicYearRepository.GetCurrentAsync(cancellationToken: cancellationToken);
        var resolvedTarget = targetSectionId ?? (await classRepository.FindNextSectionAsync(sectionId, cancellationToken))?.Id;

        if (resolvedTarget is null or 0)
        {
            return new PromotionResultDto(0, studentIds.Count, ["No next class/section is configured. Add the next class with a higher display order."]);
        }

        if (resolvedTarget == sectionId)
        {
            return new PromotionResultDto(0, studentIds.Count, ["Target section must be different from the current section."]);
        }

        var targetSection = await classRepository.GetSectionWithClassRoomAsync(resolvedTarget.Value, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Target section not found.");

        var promoted = 0;
        var skipped = 0;
        var messages = new List<string>();

        foreach (var studentId in studentIds.Distinct())
        {
            var result = await PromoteSingleAsync(
                studentId,
                currentYear.Id,
                sectionId,
                targetSection,
                source,
                promotedByUserId,
                notes,
                cancellationToken);

            if (result.Success)
            {
                promoted++;
            }
            else
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    messages.Add(result.Message);
                }
            }
        }

        if (promoted > 0 && messages.Count == 0)
        {
            messages.Add($"Promoted {promoted} student(s) to {targetSection.ClassRoom.Name}-{targetSection.Name}.");
        }

        return new PromotionResultDto(promoted, skipped, messages);
    }

    public async Task<IReadOnlyList<StudentPromotionHistoryDto>> GetRecentHistoryAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var history = await promotionRepository.GetRecentAsync(take, cancellationToken);
        return history
            .Select(x => new StudentPromotionHistoryDto(
                x.Id,
                x.Student.FullName,
                $"{x.FromSection.ClassRoom.Name}-{x.FromSection.Name}",
                $"{x.ToSection.ClassRoom.Name}-{x.ToSection.Name}",
                x.Source,
                x.PromotedAt,
                x.Notes))
            .ToList();
    }

    private async Task<(bool Success, string? Message)> PromoteSingleAsync(
        int studentId,
        int academicYearId,
        int expectedFromSectionId,
        Section targetSection,
        PromotionSource source,
        string? promotedByUserId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var enrollment = await studentRepository.GetEnrollmentAsync(studentId, academicYearId, tracking: true, cancellationToken);
        if (enrollment is null || !enrollment.IsActive)
        {
            return (false, $"Student {studentId} has no active enrollment.");
        }

        if (enrollment.SectionId != expectedFromSectionId)
        {
            return (false, $"{enrollment.Student?.FullName ?? studentId.ToString()} is not in the selected section.");
        }

        if (enrollment.SectionId == targetSection.Id)
        {
            return (false, $"{enrollment.Student?.FullName ?? studentId.ToString()} is already in the target section.");
        }

        if (await studentRepository.RollNumberExistsInSectionAsync(
                academicYearId,
                targetSection.Id,
                enrollment.RollNumber,
                studentId,
                cancellationToken))
        {
            return (false, $"Roll {enrollment.RollNumber} already exists in {targetSection.ClassRoom.Name}-{targetSection.Name}. Change roll number first.");
        }

        var fromSectionId = enrollment.SectionId;
        enrollment.SectionId = targetSection.Id;
        enrollment.UpdatedAt = DateTime.UtcNow;

        promotionRepository.Add(new StudentPromotion
        {
            StudentId = studentId,
            AcademicYearId = academicYearId,
            FromSectionId = fromSectionId,
            ToSectionId = targetSection.Id,
            Source = source,
            Notes = notes,
            PromotedByUserId = promotedByUserId,
            PromotedAt = DateTime.UtcNow
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private static StudentPromotionCandidateDto MapCandidate(StudentEnrollment enrollment, Section? nextSection) =>
        new(
            enrollment.StudentId,
            enrollment.Student.StudentCode,
            enrollment.Student.FullName,
            enrollment.RollNumber,
            enrollment.Section.ClassRoom.Name,
            enrollment.Section.Name,
            nextSection?.ClassRoom.Name,
            nextSection?.Name,
            nextSection?.Id,
            nextSection is not null);
}
