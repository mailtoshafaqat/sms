namespace SMS.Application.DTOs;

public sealed class DatabaseBackupInfo
{
    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class DatabaseBackupResult
{
    public bool Succeeded { get; set; }

    public string? FileName { get; set; }

    public string? ErrorMessage { get; set; }

    public static DatabaseBackupResult Success(string fileName) =>
        new() { Succeeded = true, FileName = fileName };

    public static DatabaseBackupResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
