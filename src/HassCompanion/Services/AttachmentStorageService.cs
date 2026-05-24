using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace HassCompanion.Services;

/// <summary>
/// Manages file storage for task attachments.
/// Resizes images to max 2048px (longest side) and saves as JPEG.
/// Files are stored on disk — path differs between dev and add-on mode.
/// </summary>
public class AttachmentStorageService
{
    private readonly string _basePath;
    private readonly ILogger<AttachmentStorageService> _logger;

    /// <summary>
    /// Max dimension (longest side) for resized images.
    /// </summary>
    private const int MaxDimension = 2048;

    /// <summary>
    /// JPEG encoding quality (0-100).
    /// </summary>
    private const int JpegQuality = 85;

    public AttachmentStorageService(IWebHostEnvironment env, ILogger<AttachmentStorageService> logger)
    {
        _logger = logger;
        _basePath = env.IsDevelopment()
            ? Path.Combine(Directory.GetCurrentDirectory(), "data", "attachments")
            : "/data/attachments";

        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Stores an uploaded image, resizing it to max 2048px.
    /// Returns the relative path for DB storage.
    /// </summary>
    public async Task<(string RelativePath, string ContentType, long FileSize)> StoreImageAsync(
        int choreTaskId, Stream imageStream, string originalFileName, CancellationToken ct = default)
    {
        var taskDir = Path.Combine(_basePath, choreTaskId.ToString());
        Directory.CreateDirectory(taskDir);

        var fileId = Guid.NewGuid().ToString("N")[..12];
        var fileName = $"{fileId}.jpg";
        var fullPath = Path.Combine(taskDir, fileName);
        var relativePath = $"{choreTaskId}/{fileName}";

        try
        {
            using var image = await Image.LoadAsync(imageStream, ct);

            // Resize if needed (maintain aspect ratio)
            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                var options = new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension)
                };
                image.Mutate(x => x.Resize(options));
            }

            // Auto-orient based on EXIF data
            image.Mutate(x => x.AutoOrient());

            // Save as JPEG
            var encoder = new JpegEncoder { Quality = JpegQuality };
            await image.SaveAsync(fullPath, encoder, ct);

            var fileSize = new FileInfo(fullPath).Length;
            _logger.LogInformation("Stored attachment: {Path} ({Size} bytes, {Width}x{Height})",
                relativePath, fileSize, image.Width, image.Height);

            return (relativePath, "image/jpeg", fileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process/store image for task {TaskId}", choreTaskId);
            throw;
        }
    }

    /// <summary>
    /// Gets the full file path for an attachment.
    /// </summary>
    public string GetFullPath(string relativePath) => Path.Combine(_basePath, relativePath);

    /// <summary>
    /// Deletes all attachments for a task.
    /// </summary>
    public void DeleteTaskAttachments(int choreTaskId)
    {
        var taskDir = Path.Combine(_basePath, choreTaskId.ToString());
        if (Directory.Exists(taskDir))
        {
            Directory.Delete(taskDir, recursive: true);
            _logger.LogInformation("Deleted attachment directory for task {TaskId}", choreTaskId);
        }
    }

    /// <summary>
    /// Deletes a single attachment file.
    /// </summary>
    public void DeleteAttachment(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Deleted attachment: {Path}", relativePath);
        }
    }
}
