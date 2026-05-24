using FamilyAssist.Data;
using FamilyAssist.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssist.Services;

/// <summary>
/// Daily background service that deletes expired task attachments.
/// Retention policy: attachments on completed tasks (Confirmed/Missed/Cancelled)
/// are deleted X days after task completion. Text comments are preserved.
/// Open tasks keep their attachments until completion + retention period.
/// </summary>
public class AttachmentCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttachmentStorageService _storage;
    private readonly ILogger<AttachmentCleanupService> _logger;

    /// <summary>
    /// AppSetting key for the retention period in days.
    /// </summary>
    public const string SettingRetentionDays = "AttachmentRetentionDays";

    /// <summary>
    /// Default retention period: 30 days after task completion.
    /// </summary>
    public const int DefaultRetentionDays = 30;

    public AttachmentCleanupService(
        IServiceScopeFactory scopeFactory,
        AttachmentStorageService storage,
        ILogger<AttachmentCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup to settle
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Attachment cleanup failed, will retry next cycle");
            }

            // Run once per day
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var retentionDays = await GetRetentionDaysAsync(db, ct);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation(
            "Running attachment cleanup: retention={Days}d, cutoff={Cutoff:u}",
            retentionDays, cutoff);

        // Find attachments on completed tasks where CompletedAt is before cutoff
        var expiredAttachments = await db.TaskAttachments
            .Include(a => a.TaskComment)
            .ThenInclude(c => c.ChoreTask)
            .Where(a =>
                a.TaskComment.ChoreTask.Status == ChoreTaskStatus.Confirmed ||
                a.TaskComment.ChoreTask.Status == ChoreTaskStatus.Missed ||
                a.TaskComment.ChoreTask.Status == ChoreTaskStatus.Cancelled)
            .Where(a => a.TaskComment.ChoreTask.CompletedAt != null &&
                        a.TaskComment.ChoreTask.CompletedAt < cutoff)
            .ToListAsync(ct);

        if (expiredAttachments.Count == 0)
        {
            _logger.LogDebug("No expired attachments found");
            return;
        }

        _logger.LogInformation("Found {Count} expired attachments to delete", expiredAttachments.Count);

        var deletedCount = 0;
        var freedBytes = 0L;

        foreach (var attachment in expiredAttachments)
        {
            try
            {
                _storage.DeleteAttachment(attachment.FilePath);
                freedBytes += attachment.FileSize;
                db.TaskAttachments.Remove(attachment);
                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete attachment {Id} at {Path}",
                    attachment.Id, attachment.FilePath);
            }
        }

        await db.SaveChangesAsync(ct);

        // Clean up empty task directories
        CleanupEmptyDirectories();

        _logger.LogInformation(
            "Attachment cleanup done: deleted {Count} files, freed {Bytes} bytes",
            deletedCount, freedBytes);
    }

    private void CleanupEmptyDirectories()
    {
        try
        {
            var basePath = _storage.BasePath;
            if (!Directory.Exists(basePath)) return;

            foreach (var dir in Directory.GetDirectories(basePath))
            {
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                    _logger.LogDebug("Removed empty directory: {Dir}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up empty directories");
        }
    }

    /// <summary>
    /// Reads retention days from DB settings (or returns default).
    /// </summary>
    public static async Task<int> GetRetentionDaysAsync(AppDbContext db, CancellationToken ct = default)
    {
        var setting = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingRetentionDays, ct);

        if (setting?.Value is not null && int.TryParse(setting.Value, out var days) && days >= 7)
            return Math.Min(days, 365);

        return DefaultRetentionDays;
    }

    /// <summary>
    /// Saves retention days to DB settings.
    /// </summary>
    public static async Task SetRetentionDaysAsync(AppDbContext db, int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 365);
        var setting = await db.AppSettings.FindAsync([SettingRetentionDays], ct);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = SettingRetentionDays, Value = days.ToString() });
        }
        else
        {
            setting.Value = days.ToString();
        }
        await db.SaveChangesAsync(ct);
    }
}
