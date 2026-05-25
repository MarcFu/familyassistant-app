using System.IO.Compression;
using System.Text.Json;
using FamilyAssistant.Data;
using FamilyAssistant.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilyAssistant.Tests;

public class BackupRestoreServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_CreatesZipWithDatabaseAndAttachments()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var dbPath = Path.Combine(dataDir, "familyassistant.db");
            var attachmentsDir = Path.Combine(dataDir, "attachments");
            Directory.CreateDirectory(Path.Combine(attachmentsDir, "42"));
            await File.WriteAllTextAsync(Path.Combine(attachmentsDir, "42", "photo.jpg"), "image-bytes");
            await CreateDatabaseAsync(dbPath, "source");

            var service = CreateService(dataDir, dbPath, attachmentsDir);
            var backup = await service.CreateBackupAsync();

            Assert.True(File.Exists(backup.FilePath));
            Assert.Equal(Path.Combine(dataDir, "backups"), Path.GetDirectoryName(backup.FilePath));

            using var archive = ZipFile.OpenRead(backup.FilePath);
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("familyassistant.db"));
            Assert.NotNull(archive.GetEntry("attachments/42/photo.jpg"));

            var manifest = await ReadManifestAsync(archive.GetEntry("manifest.json")!);
            Assert.Equal("FamilyAssistant", manifest.Application);
            Assert.True(manifest.IncludesAttachments);

            var extractedDb = Path.Combine(root, "backup.db");
            archive.GetEntry("familyassistant.db")!.ExtractToFile(extractedDb);
            Assert.Equal("source", await ReadMarkerAsync(extractedDb));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task ListBackupsAsync_ReturnsCreatedBackupsNewestFirst()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var dbPath = Path.Combine(dataDir, "familyassistant.db");
            var attachmentsDir = Path.Combine(dataDir, "attachments");
            await CreateDatabaseAsync(dbPath, "source");

            var service = CreateService(dataDir, dbPath, attachmentsDir);
            var first = await service.CreateBackupAsync();
            File.SetLastWriteTimeUtc(first.FilePath, DateTime.UtcNow.AddMinutes(-5));
            var second = await service.CreateBackupAsync();

            var backups = await service.ListBackupsAsync();

            Assert.Equal(2, backups.Count);
            Assert.Equal(second.FileName, backups[0].FileName);
            Assert.Equal(first.FileName, backups[1].FileName);
            Assert.All(backups, backup => Assert.True(backup.SizeBytes > 0));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task DeleteBackupAsync_DeletesOnlySafeBackupNames()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var dbPath = Path.Combine(dataDir, "familyassistant.db");
            var attachmentsDir = Path.Combine(dataDir, "attachments");
            await CreateDatabaseAsync(dbPath, "source");

            var service = CreateService(dataDir, dbPath, attachmentsDir);
            var backup = await service.CreateBackupAsync();

            await service.DeleteBackupAsync(backup.FileName);

            Assert.False(File.Exists(backup.FilePath));
            Assert.Empty(await service.ListBackupsAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteBackupAsync("../evil.zip"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteBackupAsync("not-a-zip.txt"));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task GetBackupFile_ReturnsExistingBackupWithoutCreatingAnotherBackup()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var dbPath = Path.Combine(dataDir, "familyassistant.db");
            var attachmentsDir = Path.Combine(dataDir, "attachments");
            await CreateDatabaseAsync(dbPath, "source");

            var service = CreateService(dataDir, dbPath, attachmentsDir);
            var created = await service.CreateBackupAsync();

            var downloaded = service.GetBackupFile(created.FileName);
            var backups = await service.ListBackupsAsync();

            Assert.Equal(created.FilePath, downloaded.FilePath);
            Assert.Single(backups);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task PrepareRestoreAsync_ValidBackupStagesPendingRestoreWithoutTouchingActiveDatabase()
    {
        var root = CreateTempRoot();
        try
        {
            var sourceDataDir = Path.Combine(root, "source");
            var sourceDbPath = Path.Combine(sourceDataDir, "familyassistant.db");
            var sourceAttachmentsDir = Path.Combine(sourceDataDir, "attachments");
            await CreateDatabaseAsync(sourceDbPath, "restored");
            await AddAttachmentAsync(sourceDbPath, sourceAttachmentsDir, "7/restore.png", ValidPngBytes);

            var backup = await CreateService(sourceDataDir, sourceDbPath, sourceAttachmentsDir).CreateBackupAsync();

            var targetDataDir = Path.Combine(root, "target");
            var targetDbPath = Path.Combine(targetDataDir, "familyassistant.db");
            var targetAttachmentsDir = Path.Combine(targetDataDir, "attachments");
            await CreateDatabaseAsync(targetDbPath, "active");

            await using var backupStream = File.OpenRead(backup.FilePath);
            var result = await CreateService(targetDataDir, targetDbPath, targetAttachmentsDir)
                .PrepareRestoreAsync(backupStream, Path.GetFileName(backup.FilePath), backupStream.Length);

            Assert.True(result.RequiresRestart);
            Assert.Equal("active", await ReadMarkerAsync(targetDbPath));
            Assert.Equal("restored", await ReadMarkerAsync(Path.Combine(targetDataDir, "restore-pending", "familyassistant.db")));
            Assert.True(File.Exists(Path.Combine(targetDataDir, "restore-pending", "attachments", "7", "restore.png")));
            Assert.Empty(Directory.EnumerateDirectories(targetDataDir, "restore-staging-*"));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task PrepareRestoreAsync_RejectsMissingReferencedAttachment()
    {
        var root = CreateTempRoot();
        try
        {
            var sourceDataDir = Path.Combine(root, "source");
            var sourceDbPath = Path.Combine(sourceDataDir, "familyassistant.db");
            var sourceAttachmentsDir = Path.Combine(sourceDataDir, "attachments");
            await CreateDatabaseAsync(sourceDbPath, "source");
            await AddAttachmentAsync(sourceDbPath, sourceAttachmentsDir, "7/missing.png", ValidPngBytes);
            File.Delete(Path.Combine(sourceAttachmentsDir, "7", "missing.png"));

            var backup = await CreateService(sourceDataDir, sourceDbPath, sourceAttachmentsDir).CreateBackupAsync();

            var targetDataDir = Path.Combine(root, "target");
            var targetDbPath = Path.Combine(targetDataDir, "familyassistant.db");
            await CreateDatabaseAsync(targetDbPath, "active");

            await using var stream = File.OpenRead(backup.FilePath);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(targetDataDir, targetDbPath, Path.Combine(targetDataDir, "attachments"))
                    .PrepareRestoreAsync(stream, backup.FileName, stream.Length));

            Assert.Contains("Attachment missing", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(targetDataDir, "restore-pending")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task PrepareRestoreAsync_RejectsUnreferencedAttachmentFile()
    {
        var root = CreateTempRoot();
        try
        {
            var sourceDataDir = Path.Combine(root, "source");
            var sourceDbPath = Path.Combine(sourceDataDir, "familyassistant.db");
            var sourceAttachmentsDir = Path.Combine(sourceDataDir, "attachments");
            await CreateDatabaseAsync(sourceDbPath, "source");
            Directory.CreateDirectory(Path.Combine(sourceAttachmentsDir, "7"));
            await File.WriteAllBytesAsync(Path.Combine(sourceAttachmentsDir, "7", "orphan.png"), ValidPngBytes);

            var backup = await CreateService(sourceDataDir, sourceDbPath, sourceAttachmentsDir).CreateBackupAsync();

            var targetDataDir = Path.Combine(root, "target");
            var targetDbPath = Path.Combine(targetDataDir, "familyassistant.db");
            await CreateDatabaseAsync(targetDbPath, "active");

            await using var stream = File.OpenRead(backup.FilePath);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(targetDataDir, targetDbPath, Path.Combine(targetDataDir, "attachments"))
                    .PrepareRestoreAsync(stream, backup.FileName, stream.Length));

            Assert.Contains("Unreferenced attachment", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(targetDataDir, "restore-pending")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task PrepareRestoreAsync_RejectsNonImageAttachmentFile()
    {
        var root = CreateTempRoot();
        try
        {
            var sourceDataDir = Path.Combine(root, "source");
            var sourceDbPath = Path.Combine(sourceDataDir, "familyassistant.db");
            var sourceAttachmentsDir = Path.Combine(sourceDataDir, "attachments");
            await CreateDatabaseAsync(sourceDbPath, "source");
            await AddAttachmentAsync(sourceDbPath, sourceAttachmentsDir, "7/not-image.png", "not an image"u8.ToArray());

            var backup = await CreateService(sourceDataDir, sourceDbPath, sourceAttachmentsDir).CreateBackupAsync();

            var targetDataDir = Path.Combine(root, "target");
            var targetDbPath = Path.Combine(targetDataDir, "familyassistant.db");
            await CreateDatabaseAsync(targetDbPath, "active");

            await using var stream = File.OpenRead(backup.FilePath);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(targetDataDir, targetDbPath, Path.Combine(targetDataDir, "attachments"))
                    .PrepareRestoreAsync(stream, backup.FileName, stream.Length));

            Assert.Contains("not a valid image", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(targetDataDir, "restore-pending")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyPendingRestore_ReplacesActiveDataAndKeepsPreRestoreBackup()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var dbPath = Path.Combine(dataDir, "familyassistant.db");
            var attachmentsDir = Path.Combine(dataDir, "attachments");
            await CreateDatabaseAsync(dbPath, "old");
            await File.WriteAllTextAsync(dbPath + "-wal", "wal");
            await File.WriteAllTextAsync(dbPath + "-shm", "shm");
            Directory.CreateDirectory(attachmentsDir);
            await File.WriteAllTextAsync(Path.Combine(attachmentsDir, "old.txt"), "old-attachment");

            var pendingDir = Path.Combine(dataDir, "restore-pending");
            await CreateDatabaseAsync(Path.Combine(pendingDir, "familyassistant.db"), "new");
            Directory.CreateDirectory(Path.Combine(pendingDir, "attachments"));
            await File.WriteAllTextAsync(Path.Combine(pendingDir, "attachments", "new.txt"), "new-attachment");

            BackupRestoreService.ApplyPendingRestore(dataDir, dbPath, NullLogger.Instance);

            Assert.Equal("new", await ReadMarkerAsync(dbPath));
            Assert.True(File.Exists(Path.Combine(attachmentsDir, "new.txt")));
            Assert.False(File.Exists(Path.Combine(attachmentsDir, "old.txt")));
            Assert.False(Directory.Exists(pendingDir));

            var backupDir = Assert.Single(Directory.EnumerateDirectories(Path.Combine(dataDir, "pre-restore-backups")));
            Assert.Equal("old", await ReadMarkerAsync(Path.Combine(backupDir, "familyassistant.db")));
            Assert.True(File.Exists(Path.Combine(backupDir, "familyassistant.db-wal")));
            Assert.True(File.Exists(Path.Combine(backupDir, "familyassistant.db-shm")));
            Assert.True(File.Exists(Path.Combine(backupDir, "attachments", "old.txt")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task PrepareRestoreAsync_RejectsUnsafeArchiveAndDoesNotStageRestore()
    {
        var root = CreateTempRoot();
        try
        {
            var zipPath = Path.Combine(root, "unsafe.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("manifest.json");
                archive.CreateEntry("attachments/../evil.txt");
            }

            var dataDir = Path.Combine(root, "data");
            var dbPath = Path.Combine(dataDir, "familyassistant.db");
            var attachmentsDir = Path.Combine(dataDir, "attachments");
            await CreateDatabaseAsync(dbPath, "active");

            await using var stream = File.OpenRead(zipPath);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(dataDir, dbPath, attachmentsDir).PrepareRestoreAsync(stream, "unsafe.zip", stream.Length));

            Assert.Contains("unsafe path", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(dataDir, "restore-pending")));
            Assert.Equal("active", await ReadMarkerAsync(dbPath));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static BackupRestoreService CreateService(string dataDir, string dbPath, string attachmentsDir) =>
        new(dataDir, dbPath, attachmentsDir, NullLogger<BackupRestoreService>.Instance);

    private static async Task CreateDatabaseAsync(string dbPath, string marker)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(builder.ToString())
            .Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Probe (Value TEXT NOT NULL);
            DELETE FROM Probe;
            INSERT INTO Probe (Value) VALUES ($marker);
            """;
        command.Parameters.AddWithValue("$marker", marker);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AddAttachmentAsync(string dbPath, string attachmentsDir, string relativePath, byte[] bytes)
    {
        var fullPath = Path.Combine(attachmentsDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes);

        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Persons (HaEntityId, Name, Role, Credits, IsPaused) VALUES ('person.test', 'Test', 0, 0, 0);
            INSERT INTO Chores (Name, CreditsReward, IsActive) VALUES ('Test chore', 1, 1);
            INSERT INTO ChoreTasks (ChoreId, DueDate, OccurrenceIndex, Status, CreditsAwarded, CreatedAt)
                VALUES (1, '2026-05-25', 1, 0, 0, '2026-05-25T00:00:00Z');
            INSERT INTO TaskComments (ChoreTaskId, PersonId, CreatedAt) VALUES (1, 1, '2026-05-25T00:00:00Z');
            INSERT INTO TaskAttachments (TaskCommentId, FileName, ContentType, FilePath, FileSize, CreatedAt)
                VALUES (1, 'restore.png', 'image/png', $path, $size, '2026-05-25T00:00:00Z');
            """;
        command.Parameters.AddWithValue("$path", relativePath);
        command.Parameters.AddWithValue("$size", bytes.LongLength);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadMarkerAsync(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Probe LIMIT 1;";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<BackupManifest> ReadManifestAsync(ZipArchiveEntry entry)
    {
        await using var stream = entry.Open();
        return (await JsonSerializer.DeserializeAsync<BackupManifest>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"familyassistant-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempRoot(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];
}
