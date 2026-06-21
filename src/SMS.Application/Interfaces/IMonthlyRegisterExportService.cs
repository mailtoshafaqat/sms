using SMS.Application.DTOs;

namespace SMS.Application.Interfaces;

public interface IMonthlyRegisterExportService
{
    byte[] BuildCsv(MonthlyRegisterDto register, RegisterExportMetadata metadata);

    byte[] BuildPdf(MonthlyRegisterDto register, RegisterExportMetadata metadata);
}

public record RegisterExportMetadata(
    string SchoolName,
    string? SchoolAddress,
    string? SchoolPhone);
