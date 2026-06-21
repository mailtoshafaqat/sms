using SMS.Application.DTOs;

namespace SMS.Application.Interfaces;

public interface IDatabaseBackupService
{
    Task<DatabaseBackupResult> CreateBackupAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseBackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);

    Task<Stream?> OpenBackupReadStreamAsync(string fileName, CancellationToken cancellationToken = default);
}
