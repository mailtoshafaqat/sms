using SMS.Domain.Enums;

namespace SMS.Application.DTOs;

public record StudentPromotionCandidateDto(
    int StudentId,
    string StudentCode,
    string FullName,
    string RollNumber,
    string CurrentClassName,
    string CurrentSectionName,
    string? NextClassName,
    string? NextSectionName,
    int? NextSectionId,
    bool CanPromote);

public record PromotionResultDto(
    int PromotedCount,
    int SkippedCount,
    IReadOnlyList<string> Messages);

public record StudentPromotionHistoryDto(
    int Id,
    string StudentName,
    string FromClass,
    string ToClass,
    PromotionSource Source,
    DateTime PromotedAt,
    string? Notes);
