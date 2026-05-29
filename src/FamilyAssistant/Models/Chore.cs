namespace FamilyAssistant.Models;

public class Chore
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Icon for this chore. Can be a Material Design icon name (e.g. "Kitchen")
    /// or an emoji character (e.g. "🧹"). Null = no icon.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Optional color for the icon as hex string (e.g. "#FFEB3B" for yellow bin).
    /// Null = default theme color.
    /// </summary>
    public string? IconColor { get; set; }

    /// <summary>
    /// Credits earned when completing this chore
    /// </summary>
    public int CreditsReward { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Soft-delete flag. Deleted chores are hidden from UI but preserved for historical data.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When this chore was soft-deleted. Null if not deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public ICollection<ChoreSchedule> Schedules { get; set; } = [];
    public ICollection<ChoreTask> Tasks { get; set; } = [];
}
