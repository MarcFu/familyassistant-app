namespace FamilyAssist.Models;

/// <summary>
/// An internet access rule that controls when and how internet/gaming is available.
/// Rules are evaluated by the InternetEnforcementService.
/// </summary>
public class InternetRule
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public required string Name { get; set; }

    /// <summary>
    /// Rule mode determines the enforcement behavior
    /// </summary>
    public InternetRuleMode RuleMode { get; set; }

    /// <summary>
    /// What this rule controls when active
    /// </summary>
    public InternetRuleTarget Target { get; set; }

    // ─── TimeWindow fields ──────────────────────────────────────

    /// <summary>
    /// Start time for TimeWindow rules (e.g., 07:00 = internet allowed from 07:00)
    /// </summary>
    public TimeOnly? WindowStart { get; set; }

    /// <summary>
    /// End time for TimeWindow rules (e.g., 21:00 = internet blocked after 21:00)
    /// </summary>
    public TimeOnly? WindowEnd { get; set; }

    /// <summary>
    /// Which days of the week this rule applies
    /// </summary>
    public DaysOfWeek ApplicableDays { get; set; } = DaysOfWeek.All;

    // ─── DetectionBudget fields (Phase 2, nullable) ─────────────

    /// <summary>
    /// Daily budget in minutes (e.g., 120 for 2h gaming)
    /// </summary>
    public int? DailyBudgetMinutes { get; set; }

    /// <summary>
    /// Minutes of detected gaming before the budget starts ticking (default: 5)
    /// </summary>
    public int? DetectionThresholdMinutes { get; set; }

    /// <summary>
    /// Grace period in minutes after budget is depleted before enforcement (default: 5)
    /// </summary>
    public int? GracePeriodMinutes { get; set; }

    /// <summary>
    /// Time of day when the daily budget resets (default: 06:00)
    /// </summary>
    public TimeOnly? BudgetResetTime { get; set; }

    // ─── Credits (Phase 3) ──────────────────────────────────────

    /// <summary>
    /// How many credits per extra minute (0 = no extra time purchasable)
    /// </summary>
    public int CreditCostPerExtraMinute { get; set; }

    // ─── Notifications ──────────────────────────────────────────

    /// <summary>
    /// Notify at these budget percentages (e.g., "75,90,100").
    /// Stored as comma-separated integers.
    /// </summary>
    public string? NotifyAtPercent { get; set; }

    // ─── State ──────────────────────────────────────────────────

    public bool IsActive { get; set; } = true;

    // ─── Navigation ─────────────────────────────────────────────

    /// <summary>
    /// Devices affected by this rule (m:n via join table)
    /// </summary>
    public ICollection<PersonDevice> AffectedDevices { get; set; } = [];
}

/// <summary>
/// The mode of an internet rule determines its enforcement behavior.
/// Values must be explicit for DB storage.
/// </summary>
public enum InternetRuleMode
{
    /// <summary>
    /// Access allowed within a specific time window (e.g., 07:00-21:00).
    /// Outside the window, enforcement activates.
    /// </summary>
    TimeWindow = 0,

    /// <summary>
    /// A daily budget of minutes, tracked via detection sensors.
    /// When depleted, enforcement activates.
    /// </summary>
    DetectionBudget = 1
}

/// <summary>
/// What the rule controls when it enforces (blocks).
/// </summary>
public enum InternetRuleTarget
{
    /// <summary>
    /// Controls full internet access (InternetSwitchEntity on device).
    /// Used for Nachtruhe-type rules.
    /// </summary>
    Internet = 0,

    /// <summary>
    /// Controls only gaming access (GamingBlockEntity on device).
    /// Used for gaming budget rules.
    /// </summary>
    Gaming = 1
}

/// <summary>
/// Replaced by InternetRuleMode - kept temporarily for migration reference.
/// </summary>
[Obsolete("Use InternetRuleMode instead")]
public enum InternetRuleType
{
    TimeWindow = 0,
    DailyBudget = 1
}

/// <summary>
/// Replaced by InternetRuleTarget - no longer needed.
/// </summary>
[Obsolete("Use PersonDevice + InternetRuleTarget instead")]
public enum InternetCategory
{
    Gaming = 0,
    Messenger = 1,
    FullAccess = 2
}

[Flags]
public enum DaysOfWeek
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    All = Weekdays | Weekend
}
