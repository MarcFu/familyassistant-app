namespace FamilyAssist.Models;

/// <summary>
/// A comment/note on a ChoreTask (thread-style).
/// Can be from the person completing the task or the reviewer.
/// </summary>
public class TaskComment
{
    public int Id { get; set; }

    /// <summary>
    /// The task this comment belongs to.
    /// </summary>
    public int ChoreTaskId { get; set; }
    public ChoreTask ChoreTask { get; set; } = null!;

    /// <summary>
    /// Who wrote this comment.
    /// </summary>
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>
    /// Text content (optional if only attachments).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// When this comment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Attachments (images) associated with this comment.
    /// </summary>
    public List<TaskAttachment> Attachments { get; set; } = [];
}
