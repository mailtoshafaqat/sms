namespace SMS.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveSchoolLogoAsync(int schoolId, Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task DeleteSchoolLogoAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<string> SaveStudentPhotoAsync(int studentId, Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task DeleteStudentPhotoAsync(string relativePath, CancellationToken cancellationToken = default);
}

