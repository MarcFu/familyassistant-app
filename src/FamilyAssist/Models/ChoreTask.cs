namespace FamilyAssist.Models;

/// <summary>
/// A concrete task instance generated from a ChoreSchedule.
/// Represents one specific occurrence that needs to be done.
/// </summary>
public class ChoreTask
{
    public int Id { get; set; }

    public int ChoreId { get; set; }
    public Chore Chore { get; set; } = null!;

    public int? ScheduleId { get; set; }
    public ChoreSchedule? Schedule { get; set; }

    /// <summary>
    /// When this task is due
    /// </summary>
    public DateOnly DueDate { get; set; }

    /// <summary>
    /// Occurrence index for multi-per-day tasks (e.g., 1 = morning, 2 = evening)
    /// </summary>
    public int OccurrenceIndex { get; set; } = 1;

    /// <summary>
    /// Label for this occurrence (e.g., "morgens", "abends") — inherited from schedule
    /// </summary>
    public string? OccurrenceLabel { get; set; }

    /// <summary>
    /// The default person (inherited from schedule at generation time)
    /// </summary>
    public int? DefaultPersonId { get; set; }
    public Person? DefaultPerson { get; set; }

    /// <summary>
    /// Who claimed/took this task (null = still open)
    /// </summary>
    public int? ClaimedByPersonId { get; set; }
    public Person? ClaimedByPerson { get; set; }

    /// <summary>
    /// Current status of this task
    /// </summary>
    public ChoreTaskStatus Status { get; set; } = ChoreTaskStatus.Open;

    /// <summary>
    /// When the task was marked as completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Who marked it as completed
    /// </summary>
    public int? CompletedByPersonId { get; set; }
    public Person? CompletedByPerson { get; set; }

    /// <summary>
    /// When a parent confirmed the completion
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// Which parent confirmed
    /// </summary>
    public int? ConfirmedByPersonId { get; set; }
    public Person? ConfirmedByPerson { get; set; }

    /// <summary>
    /// Whether credits have been awarded for this task
    /// </summary>
    public bool CreditsAwarded { get; set; }

    /// <summary>
    /// When the task was generated
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Comments/notes thread on this task.
    /// </summary>
    public List<TaskComment> Comments { get; set; } = [];
}

public enum ChoreTaskStatus
{
    /// <summary>
    /// Task is available (not yet claimed)
    /// </summary>
    Open,

    /// <summary>
    /// Someone has claimed the task
    /// </summary>
    Claimed,

    /// <summary>
    /// Child marked as done, awaiting parent confirmation
    /// </summary>
    PendingConfirmation,

    /// <summary>
    /// Parent confirmed (or parent completed directly). Credits awarded.
    /// </summary>
    Confirmed,

    /// <summary>
    /// Task was not completed and a newer task for the same schedule exists.
    /// No credits earned.
    /// </summary>
    Missed,

    /// <summary>
    /// Task was explicitly cancelled by an Admin or Parent.
    /// No credits earned. Does not respawn permanent tasks.
    /// </summary>
    Cancelled
}
