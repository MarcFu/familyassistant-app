namespace HassCompanion.Models;

public class InternetRule
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public required string Name { get; set; }

    /// <summary>
    /// Type of rule: time window or daily budget
    /// </summary>
    public InternetRuleType RuleType { get; set; }

    /// <summary>
    /// Category this rule applies to (e.g., Gaming, Messenger, Full access)
    /// </summary>
    public InternetCategory Category { get; set; }

    /// <summary>
    /// Start time for TimeWindow rules (e.g., 06:00)
    /// </summary>
    public TimeOnly? WindowStart { get; set; }

    /// <summary>
    /// End time for TimeWindow rules (e.g., 21:00)
    /// </summary>
    public TimeOnly? WindowEnd { get; set; }

    /// <summary>
    /// Daily budget in minutes for DailyBudget rules (e.g., 120 for 2h gaming)
    /// </summary>
    public int? DailyMinutes { get; set; }

    /// <summary>
    /// Which days of the week this rule applies
    /// </summary>
    public DaysOfWeek ApplicableDays { get; set; } = DaysOfWeek.All;

    /// <summary>
    /// How many credits per extra minute (0 = no extra time purchasable)
    /// </summary>
    public int CreditCostPerExtraMinute { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum InternetRuleType
{
    /// <summary>
    /// Access allowed within a specific time window
    /// </summary>
    TimeWindow,

    /// <summary>
    /// A daily budget of minutes that can be used anytime
    /// </summary>
    DailyBudget
}

public enum InternetCategory
{
    Gaming,
    Messenger,
    FullAccess
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
