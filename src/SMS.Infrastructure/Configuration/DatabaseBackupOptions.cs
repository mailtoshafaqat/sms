namespace SMS.Infrastructure.Configuration;

public sealed class DatabaseBackupOptions
{
    public const string SectionName = "DatabaseBackup";

    /// <summary>
    /// Folder relative to the web app content root where backup files are stored and listed for download.
    /// </summary>
    public string LocalDownloadDirectory { get; set; } = "App_Data/backups";

    /// <summary>
    /// Path as seen by SQL Server for BACKUP DATABASE. When empty, the physical LocalDownloadDirectory path is used.
    /// For Docker SQL Server, set this to a container path that is volume-mapped to LocalDownloadDirectory.
    /// </summary>
    public string? SqlServerBackupPath { get; set; }
}
