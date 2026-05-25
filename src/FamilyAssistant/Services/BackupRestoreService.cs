using System.IO.Compression;
using System.Text.Json;
using FamilyAssistant.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace FamilyAssistant.Services;

public sealed class BackupRestoreService
{
    public const long MaxBackupUploadBytes = 512L * 1024 * 1024;

    private const string BackupDbFileName = "familyassistant.db";
    private const string ManifestFileName = "manifest.json";
    private const string AttachmentsDirectoryName = "attachments";
    private const string BackupsDirectoryName = "backups";
    private const string PendingRestoreDirectoryName = "restore-pending";
    private const string PreRestoreBackupsDirectoryName = "pre-restore-backups";

    private readonly string _dataDir;
    private readonly string _dbPath;
    private readonly string _attachmentsPath;
    private readonly string _backupsPath;
    private readonly SemaphoreSlim _createBackupLock = new(1, 1);
    private readonly ILogger<BackupRestoreService> _logger;

    public BackupRestoreService(IWebHostEnvironment env, ILogger<BackupRestoreService> logger)
        : this(GetDataDirectory(env), GetDatabasePath(GetDataDirectory(env)), GetAttachmentsDirectory(env), logger)
    {
    }

    public BackupRestoreService(string dataDir, string dbPath, string attachmentsPath, ILogger<BackupRestoreService> logger)
    {
        _logger = logger;
        _dataDir = dataDir;
        _dbPath = dbPath;
        _attachmentsPath = attachmentsPath;
        _backupsPath = Path.Combine(_dataDir, BackupsDirectoryName);

        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_attachmentsPath);
        Directory.CreateDirectory(_backupsPath);
    }

    public static string GetDataDirectory(IHostEnvironment env) =>
        env.IsDevelopment()
            ? Path.Combine(Directory.GetCurrentDirectory(), "data")
            : "/data";

    public static string GetDatabasePath(string dataDir)
    {
        var legacyDbPath1 = Path.Combine(dataDir, "hasscompanion.db");
        var legacyDbPath2 = Path.Combine(dataDir, "familyassist.db");

        return File.Exists(legacyDbPath1)
            ? legacyDbPath1
            : File.Exists(legacyDbPath2)
                ? legacyDbPath2
                : Path.Combine(dataDir, BackupDbFileName);
    }

    public static string GetAttachmentsDirectory(IHostEnvironment env) =>
        env.IsDevelopment()
            ? Path.Combine(Directory.GetCurrentDirectory(), "data", AttachmentsDirectoryName)
            : "/data/attachments";

    public async Task<BackupFileResult> CreateBackupAsync(CancellationToken ct = default)
    {
        // BUG-003: Reject duplicate UI/browser events instead of queuing multiple backup creations.
        if (!await _createBackupLock.WaitAsync(0, ct))
            throw new InvalidOperationException("A backup is already being created.");

        try
        {
            return await CreateBackupCoreAsync(ct);
        }
        finally
        {
            _createBackupLock.Release();
        }
    }

    private async Task<BackupFileResult> CreateBackupCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_dbPath))
            throw new InvalidOperationException("Database file does not exist yet.");

        var workDir = Path.Combine(Path.GetTempPath(), $"familyassistant-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        var backupDbPath = Path.Combine(workDir, BackupDbFileName);
        var fileName = $"familyassistant-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        var tempZipPath = Path.Combine(workDir, fileName);
        var zipPath = GetUniqueBackupPath(fileName);

        try
        {
            await CopySqliteDatabaseAsync(_dbPath, backupDbPath, ct);

            var manifest = new BackupManifest(
                Application: "FamilyAssistant",
                FormatVersion: 1,
                CreatedAtUtc: DateTime.UtcNow,
                DatabaseFileName: BackupDbFileName,
                IncludesAttachments: Directory.Exists(_attachmentsPath) && Directory.EnumerateFiles(_attachmentsPath, "*", SearchOption.AllDirectories).Any());

            var manifestPath = Path.Combine(workDir, ManifestFileName);
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), ct);

            using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(manifestPath, ManifestFileName, CompressionLevel.Fastest);
                archive.CreateEntryFromFile(backupDbPath, BackupDbFileName, CompressionLevel.NoCompression);

                if (Directory.Exists(_attachmentsPath))
                {
                    var referencedFiles = await GetReferencedAttachmentFilesAsync(backupDbPath, ct);
                    AddReferencedAttachmentsToArchive(archive, _attachmentsPath, AttachmentsDirectoryName, referencedFiles, _logger);
                }
            }

            File.Move(tempZipPath, zipPath);
            _logger.LogInformation("Created backup {FileName}", Path.GetFileName(zipPath));
            return new BackupFileResult(zipPath, Path.GetFileName(zipPath), "application/zip");
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    public Task<IReadOnlyList<BackupSummary>> ListBackupsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_backupsPath))
            return Task.FromResult<IReadOnlyList<BackupSummary>>([]);

        var backups = Directory.EnumerateFiles(_backupsPath, "*.zip")
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new BackupSummary(info.Name, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc);
            })
            .OrderByDescending(b => b.LastWriteTimeUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<BackupSummary>>(backups);
    }

    public BackupFileResult GetBackupFile(string fileName)
    {
        var path = GetSafeBackupPath(fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Backup not found.", fileName);

        return new BackupFileResult(path, Path.GetFileName(path), "application/zip");
    }

    public Task DeleteBackupAsync(string fileName, CancellationToken ct = default)
    {
        var path = GetSafeBackupPath(fileName);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    public async Task<RestorePrepareResult> PrepareRestoreAsync(Stream backupStream, string fileName, long fileSize, CancellationToken ct = default)
    {
        if (fileSize <= 0)
            throw new InvalidOperationException("Backup file is empty.");

        if (fileSize > MaxBackupUploadBytes)
            throw new InvalidOperationException($"Backup file exceeds the {MaxBackupUploadBytes / 1024 / 1024} MB limit.");

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup must be a .zip file.");

        var stagingRoot = Path.Combine(_dataDir, $"restore-staging-{Guid.NewGuid():N}");
        var extractDir = Path.Combine(stagingRoot, "extract");
        var pendingDir = GetPendingRestoreDirectory(_dataDir);
        Directory.CreateDirectory(extractDir);

        try
        {
            var uploadedZipPath = Path.Combine(stagingRoot, "upload.zip");
            await using (var target = File.Create(uploadedZipPath))
            {
                await backupStream.CopyToAsync(target, ct);
            }

            using (var archive = ZipFile.OpenRead(uploadedZipPath))
            {
                ValidateArchiveEntries(archive);

                var manifestEntry = archive.GetEntry(ManifestFileName)
                    ?? throw new InvalidOperationException("Backup manifest is missing.");
                await ValidateManifestAsync(manifestEntry, ct);

                var dbEntry = archive.GetEntry(BackupDbFileName)
                    ?? throw new InvalidOperationException("Backup database is missing.");

                var extractedDbPath = Path.Combine(extractDir, BackupDbFileName);
                await ExtractEntryAsync(dbEntry, extractedDbPath, ct);

                // Query DB for referenced attachments before extracting — skip orphans
                var referencedInBackup = await GetReferencedAttachmentFilesAsync(extractedDbPath, ct);

                foreach (var entry in archive.Entries.Where(e => IsAttachmentEntry(e.FullName)))
                {
                    var relativeName = NormalizeEntryName(entry.FullName)[(AttachmentsDirectoryName.Length + 1)..];
                    if (string.IsNullOrWhiteSpace(relativeName))
                        continue;

                    if (!referencedInBackup.Contains(relativeName))
                    {
                        _logger.LogWarning("Skipping orphaned attachment during restore (no DB record): {File}", relativeName);
                        continue;
                    }

                    await ExtractEntryAsync(entry, Path.Combine(extractDir, AttachmentsDirectoryName, relativeName), ct);
                }

                await ValidateDatabaseAsync(extractedDbPath, ct);
                await ValidateAttachmentReferencesAsync(extractedDbPath, Path.Combine(extractDir, AttachmentsDirectoryName), _logger, ct);
                await ValidateDatabaseCanMigrateAsync(extractedDbPath, ct);
            }

            var marker = new PendingRestoreMarker(fileName, DateTime.UtcNow);
            await File.WriteAllTextAsync(
                Path.Combine(extractDir, "restore-pending.json"),
                JsonSerializer.Serialize(marker, JsonOptions),
                ct);

            if (Directory.Exists(pendingDir))
                Directory.Delete(pendingDir, recursive: true);
            Directory.Move(extractDir, pendingDir);

            _logger.LogWarning("Restore prepared from {FileName}; restart required to apply it", fileName);
            return new RestorePrepareResult(true, "Restore prepared. Restart the add-on/app to apply it.");
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    public static void ApplyPendingRestore(string dataDir, string dbPath, ILogger logger)
    {
        var pendingDir = GetPendingRestoreDirectory(dataDir);
        var pendingDbPath = Path.Combine(pendingDir, BackupDbFileName);

        if (!File.Exists(pendingDbPath))
            return;

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var preRestoreBackupDir = Path.Combine(dataDir, PreRestoreBackupsDirectoryName, timestamp);
        Directory.CreateDirectory(preRestoreBackupDir);

        logger.LogWarning("Applying pending database restore from {PendingDir}", pendingDir);

        BackupExistingDatabaseFiles(dbPath, preRestoreBackupDir, logger);
        ReplaceDatabaseFile(pendingDbPath, dbPath);

        var activeAttachmentsDir = Path.Combine(dataDir, AttachmentsDirectoryName);
        var pendingAttachmentsDir = Path.Combine(pendingDir, AttachmentsDirectoryName);
        ReplaceAttachmentsDirectory(activeAttachmentsDir, pendingAttachmentsDir, preRestoreBackupDir, logger);

        Directory.Delete(pendingDir, recursive: true);
        logger.LogWarning("Pending restore applied. Previous data was saved to {BackupDir}", preRestoreBackupDir);
    }

    private static async Task CopySqliteDatabaseAsync(string sourcePath, string targetPath, CancellationToken ct)
    {
        var sourceBuilder = new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        // BUG-001: Disable pooling so Windows releases the temporary backup DB before it is zipped.
        var targetBuilder = new SqliteConnectionStringBuilder { DataSource = targetPath, Pooling = false };

        await using var source = new SqliteConnection(sourceBuilder.ToString());
        await using var target = new SqliteConnection(targetBuilder.ToString());
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        source.BackupDatabase(target);
    }

    private static async Task ValidateDatabaseAsync(string dbPath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(ct);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check;";
            var result = await command.ExecuteScalarAsync(ct) as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Backup database integrity check failed: {result}");
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (count == 0)
                throw new InvalidOperationException("Backup does not look like a FamilyAssistant database.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";
            var knownMigrations = GetKnownMigrationIds();
            var unknownMigrations = new List<string>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var migrationId = reader.GetString(0);
                if (!knownMigrations.Contains(migrationId))
                    unknownMigrations.Add(migrationId);
            }

            if (unknownMigrations.Count > 0)
                throw new InvalidOperationException($"Backup contains unknown migrations: {string.Join(", ", unknownMigrations)}");
        }
    }

    private static async Task ValidateDatabaseCanMigrateAsync(string dbPath, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"familyassistant-restore-migrate-{Guid.NewGuid():N}.db");

        try
        {
            File.Copy(dbPath, tempPath, overwrite: true);
            var builder = new SqliteConnectionStringBuilder { DataSource = tempPath, Pooling = false };
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(builder.ToString())
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync(ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Backup database cannot be migrated: {ex.Message}", ex);
        }
        finally
        {
            foreach (var path in new[] { tempPath, tempPath + "-wal", tempPath + "-shm" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }

    private static async Task ValidateAttachmentReferencesAsync(string dbPath, string attachmentsDir, ILogger logger, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(ct);

        var attachmentFiles = Directory.Exists(attachmentsDir)
            ? Directory.EnumerateFiles(attachmentsDir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(attachmentsDir, path).Replace(Path.DirectorySeparatorChar, '/'))
                .ToHashSet(StringComparer.Ordinal)
            : [];

        if (!await TableExistsAsync(connection, "TaskAttachments", ct))
        {
            if (attachmentFiles.Count > 0)
                throw new InvalidOperationException("Backup contains attachments but the database has no TaskAttachments table.");

            return;
        }

        var expectedFiles = new Dictionary<string, AttachmentReference>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id, FilePath, ContentType, FileSize FROM TaskAttachments;";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var attachment = new AttachmentReference(
                    Id: reader.GetInt32(0),
                    FilePath: reader.GetString(1),
                    ContentType: reader.GetString(2),
                    FileSize: reader.GetInt64(3));

                ValidateAttachmentPath(attachment.FilePath, attachment.Id);

                if (!attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Attachment is not an image content type: {attachment.FilePath}");

                if (!expectedFiles.TryAdd(attachment.FilePath, attachment))
                    throw new InvalidOperationException($"Duplicate attachment path in database: {attachment.FilePath}");
            }
        }

        foreach (var expected in expectedFiles.Values)
        {
            var filePath = Path.Combine(attachmentsDir, expected.FilePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
                throw new InvalidOperationException($"Attachment missing: attachments/{expected.FilePath}");

            var fileInfo = new FileInfo(filePath);
            if (expected.FileSize >= 0 && fileInfo.Length != expected.FileSize)
            {
                throw new InvalidOperationException(
                    $"Attachment size mismatch: attachments/{expected.FilePath} (DB: {expected.FileSize}, file: {fileInfo.Length})");
            }

            try
            {
                var imageInfo = await Image.IdentifyAsync(filePath, ct);
                if (imageInfo is null)
                    throw new InvalidOperationException($"Attachment is not a valid image: attachments/{expected.FilePath}");
            }
            catch (UnknownImageFormatException ex)
            {
                throw new InvalidOperationException($"Attachment is not a valid image: attachments/{expected.FilePath}", ex);
            }
        }

        foreach (var actualFile in attachmentFiles)
        {
            if (!expectedFiles.ContainsKey(actualFile))
                logger.LogWarning("Orphaned attachment in backup (no DB record): attachments/{File} — will be ignored", actualFile);
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static void ValidateAttachmentPath(string filePath, int attachmentId)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || filePath.Contains('\\')
            || Path.IsPathRooted(filePath)
            || filePath.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part is "." or ".."))
        {
            throw new InvalidOperationException($"Attachment {attachmentId} has an unsafe file path: {filePath}");
        }
    }

    private static HashSet<string> GetKnownMigrationIds()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new AppDbContext(options);
        return db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    }

    private static async Task ValidateManifestAsync(ZipArchiveEntry manifestEntry, CancellationToken ct)
    {
        await using var stream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Backup manifest is invalid.");

        if (!string.Equals(manifest.Application, "FamilyAssistant", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup was not created by FamilyAssistant.");

        if (manifest.FormatVersion != 1)
            throw new InvalidOperationException($"Unsupported backup format version: {manifest.FormatVersion}");
    }

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeEntryName(entry.FullName);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(part => part is ".." or "."))
                throw new InvalidOperationException($"Backup contains an unsafe path: {entry.FullName}");

            if (normalized != ManifestFileName
                && normalized != BackupDbFileName
                && !normalized.StartsWith(AttachmentsDirectoryName + "/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Backup contains an unexpected file: {entry.FullName}");
            }
        }
    }

    private static async Task ExtractEntryAsync(ZipArchiveEntry entry, string targetPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source = entry.Open();
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, ct);
    }

    private static bool IsAttachmentEntry(string entryName)
    {
        var normalized = NormalizeEntryName(entryName);
        return normalized.StartsWith(AttachmentsDirectoryName + "/", StringComparison.Ordinal)
            && !normalized.EndsWith("/", StringComparison.Ordinal);
    }

    private static string NormalizeEntryName(string entryName) => entryName.Replace('\\', '/').TrimStart('/');

    private string GetUniqueBackupPath(string fileName)
    {
        Directory.CreateDirectory(_backupsPath);
        var path = Path.Combine(_backupsPath, fileName);
        if (!File.Exists(path))
            return path;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(_backupsPath, $"{name}-{Guid.NewGuid().ToString("N")[..8]}{extension}");
    }

    private string GetSafeBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName != Path.GetFileName(fileName)
            || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid backup file name.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_backupsPath, fileName));
        var backupsRoot = Path.GetFullPath(_backupsPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(backupsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid backup file name.");

        return fullPath;
    }

    private static void AddReferencedAttachmentsToArchive(
        ZipArchive archive, string sourceDir, string archiveRoot, HashSet<string> referencedFiles, ILogger logger)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file).Replace(Path.DirectorySeparatorChar, '/');
            if (referencedFiles.Contains(relativePath))
            {
                archive.CreateEntryFromFile(file, $"{archiveRoot}/{relativePath}", CompressionLevel.Fastest);
            }
            else
            {
                logger.LogWarning("Skipping orphaned attachment (no DB record): {File}", relativePath);
            }
        }
    }

    private static async Task<HashSet<string>> GetReferencedAttachmentFilesAsync(string dbPath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(ct);

        var files = new HashSet<string>(StringComparer.Ordinal);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FilePath FROM TaskAttachments;";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            files.Add(reader.GetString(0));
        }

        return files;
    }

    private static string GetPendingRestoreDirectory(string dataDir) => Path.Combine(dataDir, PendingRestoreDirectoryName);

    private static void BackupExistingDatabaseFiles(string dbPath, string backupDir, ILogger logger)
    {
        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (!File.Exists(path))
                continue;

            File.Copy(path, Path.Combine(backupDir, Path.GetFileName(path)), overwrite: true);
            File.Delete(path);
            logger.LogInformation("Backed up and removed active database file {Path}", path);
        }
    }

    private static void ReplaceDatabaseFile(string pendingDbPath, string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        File.Copy(pendingDbPath, dbPath, overwrite: true);
    }

    private static void ReplaceAttachmentsDirectory(string activeDir, string pendingDir, string backupDir, ILogger logger)
    {
        if (Directory.Exists(activeDir))
        {
            var backupAttachmentsDir = Path.Combine(backupDir, AttachmentsDirectoryName);
            Directory.Move(activeDir, backupAttachmentsDir);
            logger.LogInformation("Backed up active attachments to {Path}", backupAttachmentsDir);
        }

        if (Directory.Exists(pendingDir))
        {
            CopyDirectory(pendingDir, activeDir);
        }
        else
        {
            Directory.CreateDirectory(activeDir);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetPath = Path.Combine(targetDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

public sealed record BackupFileResult(string FilePath, string FileName, string ContentType);

public sealed record BackupSummary(string FileName, long SizeBytes, DateTime CreatedAtUtc, DateTime LastWriteTimeUtc);

public sealed record RestorePrepareResult(bool RequiresRestart, string Message);

public sealed record BackupManifest(
    string Application,
    int FormatVersion,
    DateTime CreatedAtUtc,
    string DatabaseFileName,
    bool IncludesAttachments);

public sealed record PendingRestoreMarker(string OriginalFileName, DateTime PreparedAtUtc);

file sealed record AttachmentReference(int Id, string FilePath, string ContentType, long FileSize);
