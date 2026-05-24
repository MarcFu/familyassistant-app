namespace HassCompanion.Models;

/// <summary>
/// An image/file attachment on a TaskComment.
/// Stored on disk, referenced by path.
/// </summary>
public class TaskAttachment
{
    public int Id { get; set; }

    /// <summary>
    /// The comment this attachment belongs to.
    /// </summary>
    public int TaskCommentId { get; set; }
    public TaskComment TaskComment { get; set; } = null!;

    /// <summary>
    /// Original file name (e.g., "katzenklo.jpg").
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// MIME type (e.g., "image/jpeg", "image/png").
    /// </summary>
    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>
    /// Relative file path on disk (e.g., "attachments/42/a1b2c3d4.jpg").
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// File size in bytes (after resize).
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// When this attachment was uploaded.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
