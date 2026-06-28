using SMS.Application.DTOs;

namespace SMS.Application.Interfaces;

public interface IStaffMonthlyRegisterExportService
{
    byte[] BuildCsv(StaffMonthlyRegisterDto register, RegisterExportMetadata metadata);

    byte[] BuildPdf(StaffMonthlyRegisterDto register, RegisterExportMetadata metadata);
}
