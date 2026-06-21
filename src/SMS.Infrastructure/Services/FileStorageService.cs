using Microsoft.AspNetCore.Hosting;
using SMS.Application.Interfaces;

namespace SMS.Infrastructure.Services;

public class FileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    private const long MaxImageSizeBytes = 2 * 1024 * 1024;

    public Task<string> SaveSchoolLogoAsync(int schoolId, Stream fileStream, string fileName, CancellationToken cancellationToken = default) =>
        SaveImageAsync("school", $"logo_{schoolId}", fileStream, fileName, cancellationToken);

    public Task DeleteSchoolLogoAsync(string relativePath, CancellationToken cancellationToken = default) =>
        DeleteImageAsync(relativePath);

    public Task<string> SaveStudentPhotoAsync(int studentId, Stream fileStream, string fileName, CancellationToken cancellationToken = default) =>
        SaveImageAsync("students", $"student_{studentId}", fileStream, fileName, cancellationToken);

    public Task DeleteStudentPhotoAsync(string relativePath, CancellationToken cancellationToken = default) =>
        DeleteImageAsync(relativePath);

    private async Task<string> SaveImageAsync(
        string folder,
        string baseFileName,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only PNG, JPG, JPEG, WEBP, or GIF image files are allowed.");
        }

        if (fileStream.CanSeek && fileStream.Length > MaxImageSizeBytes)
        {
            throw new InvalidOperationException("Image file must be 2 MB or smaller.");
        }

        var uploadDir = Path.Combine(environment.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadDir);

        var storedFileName = $"{baseFileName}{extension.ToLowerInvariant()}";
        var physicalPath = Path.Combine(uploadDir, storedFileName);

        await using var output = File.Create(physicalPath);
        await fileStream.CopyToAsync(output, cancellationToken);

        return $"/uploads/{folder}/{storedFileName}";
    }

    private Task DeleteImageAsync(string relativePath)
    {
        var physicalPath = Path.Combine(
            environment.WebRootPath,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}
