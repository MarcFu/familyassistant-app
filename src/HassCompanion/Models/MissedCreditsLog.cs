namespace HassCompanion.Models;

/// <summary>
/// Audit log entry for credits that were not earned due to missed tasks.
/// Used for Taschengeld negotiations.
/// </summary>
public class MissedCreditsLog
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public int ChoreTaskId { get; set; }
    public ChoreTask ChoreTask { get; set; } = null!;

    public int ScheduleId { get; set; }
    public ChoreSchedule Schedule { get; set; } = null!;

    /// <summary>
    /// How many credits could have been earned
    /// </summary>
    public int CreditsNotEarned { get; set; }

    /// <summary>
    /// Why the credits were missed
    /// </summary>
    public MissedReason Reason { get; set; }

    /// <summary>
    /// If true, the person was paused (vacation) — does NOT count as shame
    /// </summary>
    public bool WasPaused { get; set; }

    public DateOnly Date { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum MissedReason
{
    /// <summary>
    /// Task was not completed before it expired (new task generated for same schedule)
    /// </summary>
    Expired,

    /// <summary>
    /// Task was never claimed/completed
    /// </summary>
    NotDone
}
