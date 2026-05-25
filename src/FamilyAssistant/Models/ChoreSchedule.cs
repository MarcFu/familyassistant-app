namespace FamilyAssistant.Models;

/// <summary>
/// Defines a recurring schedule for a chore (e.g., "Emi feeds cats daily 2x", "Louanne litter box Mon/Wed/Fri/Sun")
/// </summary>
public class ChoreSchedule
{
    public int Id { get; set; }

    public int ChoreId { get; set; }
    public Chore Chore { get; set; } = null!;

    /// <summary>
    /// Default person assigned to this schedule (null = open for anyone)
    /// </summary>
    public int? DefaultPersonId { get; set; }
    public Person? DefaultPerson { get; set; }

    /// <summary>
    /// How the schedule recurs
    /// </summary>
    public ScheduleRhythm Rhythm { get; set; }

    /// <summary>
    /// For XTimesPerWeek: how many times per week (e.g., 2)
    /// </summary>
    public int? TimesPerWeek { get; set; }

    /// <summary>
    /// For EveryXMonths: interval in months (e.g., 3 = every 3 months)
    /// </summary>
    public int? IntervalMonths { get; set; }

    /// <summary>
    /// For EveryXWeeks: interval in weeks (e.g., 2 = every 2 weeks)
    /// </summary>
    public int? IntervalWeeks { get; set; }

    /// <summary>
    /// For SpecificDays: which days of the week
    /// </summary>
    public DaysOfWeek? SpecificDays { get; set; }

    /// <summary>
    /// How many times per day this task needs to be done (e.g., 2 for "feed cats morning + evening")
    /// </summary>
    public int TimesPerDay { get; set; } = 1;

    /// <summary>
    /// Optional label for time-of-day distinction (e.g., "morgens", "abends")
    /// </summary>
    public string? TimeLabel { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum ScheduleRhythm
{
    /// <summary>
    /// Every day
    /// </summary>
    Daily = 0,

    /// <summary>
    /// On specific days of the week (e.g., Mon/Wed/Fri)
    /// </summary>
    SpecificDays = 1,

    /// <summary>
    /// X times per week (flexible which days)
    /// </summary>
    XTimesPerWeek = 2,

    /// <summary>
    /// Once per week
    /// </summary>
    Weekly = 3,

    /// <summary>
    /// Once per month
    /// </summary>
    Monthly = 4,

    /// <summary>
    /// Every X months (e.g., every 3 months for deep cleaning).
    /// Uses IntervalMonths property for the interval.
    /// </summary>
    EveryXMonths = 5,

    /// <summary>
    /// Always available — when completed, a new task is immediately created.
    /// Used for things like "Geschirr einräumen" that can be done anytime, multiple times per day.
    /// </summary>
    Permanent = 6,

    /// <summary>
    /// Every X weeks (e.g., every 2 weeks for biweekly tasks).
    /// Uses IntervalWeeks property for the interval.
    /// </summary>
    EveryXWeeks = 7
}
