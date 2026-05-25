namespace FamilyAssistant.Models;

/// <summary>
/// Configuration for an event trigger: when a HA entity reaches a certain state,
/// automatically create an ad-hoc task for a configured chore.
/// </summary>
public class EventTrigger
{
    public int Id { get; set; }

    /// <summary>
    /// HA entity ID to monitor (e.g. "sensor.cat_litter_weight", "binary_sensor.mailbox").
    /// </summary>
    public string EntityId { get; set; } = "";

    /// <summary>
    /// Friendly name for this trigger (shown in UI).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The state value that fires the trigger (e.g. "on", "below_threshold", "open").
    /// Compared case-insensitively against the HA entity state.
    /// </summary>
    public string TriggerState { get; set; } = "";

    /// <summary>
    /// Which chore to create a task for when triggered.
    /// </summary>
    public int ChoreId { get; set; }
    public Chore Chore { get; set; } = null!;

    /// <summary>
    /// Whether this trigger is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Debounce period in minutes — ignore repeated triggers within this window.
    /// Prevents state flapping from creating duplicate tasks.
    /// </summary>
    public int DebounceMins { get; set; } = 60;

    /// <summary>
    /// Last time this trigger fired (for debounce calculation).
    /// </summary>
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>
    /// When this trigger was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
