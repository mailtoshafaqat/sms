using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Infrastructure.Configuration;

namespace SMS.Infrastructure.Services;

public sealed class DatabaseBackupService(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<DatabaseBackupOptions> options,
    ILogger<DatabaseBackupService> logger) : IDatabaseBackupService
{
    private const string DockerBackupPath = "/var/opt/mssql/backup";
    private readonly DatabaseBackupOptions _options = options.Value;

    public async Task<DatabaseBackupResult> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return DatabaseBackupResult.Failure("Database connection string is not configured.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return DatabaseBackupResult.Failure("Database name is missing from the connection string.");
        }

        var localDirectory = GetLocalBackupDirectory();
        Directory.CreateDirectory(localDirectory);

        var fileName = $"{SanitizeFileName(databaseName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var localFilePath = Path.Combine(localDirectory, fileName);
        var sqlServerPath = ResolveSqlServerBackupPath(localFilePath, fileName, builder);

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var backupSql = """
                BACKUP DATABASE [{0}]
                TO DISK = @backupPath
                WITH FORMAT, INIT, NAME = N'{0} Manual Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10;
                """;

            await using var command = new SqlCommand(string.Format(backupSql, databaseName), connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = 300
            };
            command.Parameters.AddWithValue("@backupPath", sqlServerPath);

            await command.ExecuteNonQueryAsync(cancellationToken);

            if (!File.Exists(localFilePath))
            {
                logger.LogInformation(
                    "Backup file not found locally at {LocalPath}. Pulling from SQL Server path {SqlPath}.",
                    localFilePath,
                    sqlServerPath);

                var pulled = await TryPullBackupFromServerAsync(connection, sqlServerPath, localFilePath, cancellationToken);
                if (!pulled || !File.Exists(localFilePath))
                {
                    return DatabaseBackupResult.Failure(
                        "Backup was created on SQL Server but could not be copied to the web app. " +
                        $"SQL path: {sqlServerPath}. Local folder: {localDirectory}. " +
                        "For Docker SQL Server, set DatabaseBackup:SqlServerBackupPath to /var/opt/mssql/backup " +
                        "and mount src/SMS.Web/App_Data/backups to that folder, or use docker-compose.yml in the project root.");
                }
            }

            return DatabaseBackupResult.Success(fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database backup failed for {DatabaseName}.", databaseName);
            return DatabaseBackupResult.Failure(ex.Message);
        }
    }

    public Task<IReadOnlyList<DatabaseBackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var localDirectory = GetLocalBackupDirectory();
        if (!Directory.Exists(localDirectory))
        {
            return Task.FromResult<IReadOnlyList<DatabaseBackupInfo>>(Array.Empty<DatabaseBackupInfo>());
        }

        var backups = Directory.GetFiles(localDirectory, "*.bak")
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new DatabaseBackupInfo
                {
                    FileName = info.Name,
                    SizeBytes = info.Length,
                    CreatedUtc = info.LastWriteTimeUtc
                };
            })
            .OrderByDescending(x => x.CreatedUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<DatabaseBackupInfo>>(backups);
    }

    public Task<Stream?> OpenBackupReadStreamAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!IsValidBackupFileName(fileName))
        {
            return Task.FromResult<Stream?>(null);
        }

        var localDirectory = GetLocalBackupDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(localDirectory, fileName));
        if (!fullPath.StartsWith(Path.GetFullPath(localDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<Stream?>(null);
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(stream);
    }

    private string GetLocalBackupDirectory()
    {
        var relative = string.IsNullOrWhiteSpace(_options.LocalDownloadDirectory)
            ? "App_Data/backups"
            : _options.LocalDownloadDirectory;

        return Path.Combine(environment.ContentRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ResolveSqlServerBackupPath(string localFilePath, string fileName, SqlConnectionStringBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(_options.SqlServerBackupPath))
        {
            var sqlRoot = _options.SqlServerBackupPath.TrimEnd('\\', '/');
            return $"{sqlRoot}/{fileName}";
        }

        if (IsLikelyDockerSqlServer(builder))
        {
            return $"{DockerBackupPath}/{fileName}";
        }

        return localFilePath;
    }

    private static bool IsLikelyDockerSqlServer(SqlConnectionStringBuilder builder)
    {
        var dataSource = builder.DataSource ?? string.Empty;
        return dataSource.Contains("14331", StringComparison.Ordinal)
            || dataSource.Contains(",1433", StringComparison.Ordinal);
    }

    private async Task<bool> TryPullBackupFromServerAsync(
        SqlConnection connection,
        string sqlServerPath,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await TryEnableAdHocDistributedQueriesAsync(connection, cancellationToken);

            await using var command = new SqlCommand(
                "SELECT BulkColumn FROM OPENROWSET(BULK @path, SINGLE_BLOB) AS BackupFile;",
                connection)
            {
                CommandTimeout = 300
            };
            command.Parameters.AddWithValue("@path", sqlServerPath);

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            {
                return false;
            }

            await using var source = reader.GetStream(0);
            await using var destination = File.Create(localFilePath);
            await source.CopyToAsync(destination, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not pull backup file from SQL Server path {SqlPath}.", sqlServerPath);
            return false;
        }
    }

    private static async Task TryEnableAdHocDistributedQueriesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var commands = new[]
        {
            "EXEC sp_configure 'show advanced options', 1; RECONFIGURE;",
            "EXEC sp_configure 'Ad Hoc Distributed Queries', 1; RECONFIGURE;"
        };

        foreach (var sql in commands)
        {
            try
            {
                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Best effort only; OPENROWSET may already be enabled or not permitted.
            }
        }
    }

    private static bool IsValidBackupFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && !fileName.Contains("..", StringComparison.Ordinal)
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string value) =>
        string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
}
