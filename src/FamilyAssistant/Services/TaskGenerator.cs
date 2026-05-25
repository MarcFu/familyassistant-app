using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

/// <summary>
/// Scoped service that generates ChoreTask instances from ChoreSchedules.
/// Can be called by the background service (hourly) or manually from the UI.
/// Idempotent: won't create duplicate tasks.
/// </summary>
public class TaskGenerator
{
    private readonly AppDbContext _db;
    private readonly ILogger<TaskGenerator> _logger;
    private const int WeeksAhead = 4;

    public TaskGenerator(AppDbContext db, ILogger<TaskGenerator> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Generates tasks for all active schedules (4 weeks ahead) and processes missed tasks.
    /// Returns the number of newly created tasks.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var created = await GenerateTasksAsync(ct);
        await ProcessMissedTasksAsync(ct);
        return created;
    }

    private async Task<int> GenerateTasksAsync(CancellationToken ct)
    {
        var activeSchedules = await _db.ChoreSchedules
            .Include(s => s.Chore)
            .Include(s => s.DefaultPerson)
            .Where(s => s.IsActive && s.Chore.IsActive)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var endDate = today.AddDays(WeeksAhead * 7);

        var createdCount = 0;

        foreach (var schedule in activeSchedules)
        {
            // Permanent schedules: ensure exactly 1 open task exists
            if (schedule.Rhythm == ScheduleRhythm.Permanent)
            {
                createdCount += await EnsurePermanentTaskAsync(schedule, today, ct);
                continue;
            }

            var dates = GetScheduledDates(schedule, today, endDate);

            foreach (var date in dates)
            {
                for (int occurrence = 1; occurrence <= schedule.TimesPerDay; occurrence++)
                {
                    var exists = await _db.ChoreTasks.AnyAsync(t =>
                        t.ScheduleId == schedule.Id &&
                        t.DueDate == date &&
                        t.OccurrenceIndex == occurrence, ct);

                    if (exists) continue;

                    var defaultPersonId = schedule.DefaultPersonId;
                    if (defaultPersonId.HasValue && schedule.DefaultPerson?.IsPaused == true)
                    {
                        defaultPersonId = null;
                    }

                    var task = new ChoreTask
                    {
                        ChoreId = schedule.ChoreId,
                        ScheduleId = schedule.Id,
                        DueDate = date,
                        OccurrenceIndex = occurrence,
                        OccurrenceLabel = schedule.TimesPerDay > 1 ? $"{occurrence}. Mal" : null,
                        DefaultPersonId = defaultPersonId,
                        Status = ChoreTaskStatus.Open,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.ChoreTasks.Add(task);
                    createdCount++;
                }
            }
        }

        if (createdCount > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Generated {Count} new chore tasks", createdCount);
        }

        return createdCount;
    }

    /// <summary>
    /// For Permanent schedules: ensure there's always exactly 1 open task.
    /// Returns 1 if a new task was created, 0 otherwise.
    /// </summary>
    private async Task<int> EnsurePermanentTaskAsync(ChoreSchedule schedule, DateOnly today, CancellationToken ct)
    {
        var hasOpenTask = await _db.ChoreTasks.AnyAsync(t =>
            t.ScheduleId == schedule.Id &&
            (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed), ct);

        if (hasOpenTask) return 0;

        var defaultPersonId = schedule.DefaultPersonId;
        if (defaultPersonId.HasValue && schedule.DefaultPerson?.IsPaused == true)
        {
            defaultPersonId = null;
        }

        var task = new ChoreTask
        {
            ChoreId = schedule.ChoreId,
            ScheduleId = schedule.Id,
            DueDate = today,
            OccurrenceIndex = 1,
            OccurrenceLabel = null,
            DefaultPersonId = defaultPersonId,
            Status = ChoreTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChoreTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return 1;
    }

    /// <summary>
    /// Called after a permanent task is completed — immediately spawns the next one.
    /// </summary>
    public async Task RespawnPermanentTaskAsync(ChoreTask completedTask, CancellationToken ct = default)
    {
        if (completedTask.Schedule?.Rhythm != ScheduleRhythm.Permanent) return;

        var schedule = completedTask.Schedule;
        await EnsurePermanentTaskAsync(schedule, DateOnly.FromDateTime(DateTime.Today), ct);
    }

    private async Task ProcessMissedTasksAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // --- Ad-hoc tasks (no schedule): mark missed if past due date ---
        var overdueAdHoc = await _db.ChoreTasks
            .Include(t => t.Chore)
            .Where(t => t.DueDate < today
                && (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed)
                && t.ScheduleId == null)
            .ToListAsync(ct);

        foreach (var task in overdueAdHoc)
        {
            task.Status = ChoreTaskStatus.Missed;
        }

        // --- Scheduled tasks (exclude Permanent): mark missed based on newer task or next occurrence ---
        var overdueTasks = await _db.ChoreTasks
            .Include(t => t.Chore)
            .Include(t => t.Schedule)
            .Where(t => t.DueDate < today
                && (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed)
                && t.ScheduleId != null
                && t.Schedule!.Rhythm != ScheduleRhythm.Permanent)
            .ToListAsync(ct);

        foreach (var task in overdueTasks)
        {
            // For non-daily rhythms, only mark as missed once the next occurrence date has arrived.
            // This gives weekly tasks the full week, monthly tasks the full month, etc.
            if (task.Schedule!.Rhythm != ScheduleRhythm.Daily && task.Schedule.Rhythm != ScheduleRhythm.SpecificDays)
            {
                var nextDueDate = await _db.ChoreTasks
                    .Where(t => t.ScheduleId == task.ScheduleId &&
                                t.OccurrenceIndex == task.OccurrenceIndex &&
                                t.DueDate > task.DueDate)
                    .Select(t => t.DueDate)
                    .OrderBy(d => d)
                    .FirstOrDefaultAsync(ct);

                // Not missed yet — the next occurrence hasn't started
                if (nextDueDate != default && today < nextDueDate)
                    continue;
            }

            var newerTaskExists = await _db.ChoreTasks.AnyAsync(t =>
                t.ScheduleId == task.ScheduleId &&
                t.OccurrenceIndex == task.OccurrenceIndex &&
                t.DueDate > task.DueDate &&
                t.Id != task.Id, ct);

            if (newerTaskExists)
            {
                task.Status = ChoreTaskStatus.Missed;

                var responsiblePersonId = task.ClaimedByPersonId ?? task.DefaultPersonId;

                if (responsiblePersonId.HasValue)
                {
                    var person = await _db.Persons.FindAsync([responsiblePersonId.Value], ct);
                    var wasPaused = person?.IsPaused ?? false;

                    var log = new MissedCreditsLog
                    {
                        PersonId = responsiblePersonId.Value,
                        ChoreTaskId = task.Id,
                        ScheduleId = task.ScheduleId!.Value,
                        CreditsNotEarned = task.Chore.CreditsReward,
                        Reason = task.Status == ChoreTaskStatus.Claimed ? MissedReason.Expired : MissedReason.NotDone,
                        WasPaused = wasPaused,
                        Date = task.DueDate
                    };

                    _db.MissedCreditsLogs.Add(log);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static List<DateOnly> GetScheduledDates(ChoreSchedule schedule, DateOnly from, DateOnly to)
    {
        var dates = new List<DateOnly>();

        switch (schedule.Rhythm)
        {
            case ScheduleRhythm.Daily:
                for (var d = from; d <= to; d = d.AddDays(1))
                    dates.Add(d);
                break;

            case ScheduleRhythm.SpecificDays:
                if (schedule.SpecificDays is null) break;
                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    if (DayMatchesFlag(d.DayOfWeek, schedule.SpecificDays.Value))
                        dates.Add(d);
                }
                break;

            case ScheduleRhythm.XTimesPerWeek:
                var timesPerWeek = schedule.TimesPerWeek ?? 1;
                var spacing = 7.0 / timesPerWeek;
                for (var d = from; d <= to;)
                {
                    var weekStart = d.AddDays(-(int)d.DayOfWeek + (int)DayOfWeek.Monday);
                    for (int i = 0; i < timesPerWeek; i++)
                    {
                        var taskDay = weekStart.AddDays((int)(i * spacing));
                        if (taskDay >= from && taskDay <= to)
                            dates.Add(taskDay);
                    }
                    d = weekStart.AddDays(7);
                }
                dates = dates.Distinct().ToList();
                break;

            case ScheduleRhythm.Weekly:
                // Find next Monday (or today if already Monday).
                // Note: DayOfWeek.Sunday == 0, so we handle it explicitly to avoid skipping the next day.
                int daysUntilMonday;
                if (from.DayOfWeek == DayOfWeek.Monday)
                    daysUntilMonday = 0;
                else if (from.DayOfWeek == DayOfWeek.Sunday)
                    daysUntilMonday = 1;
                else
                    daysUntilMonday = ((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7;

                var startDay = from.AddDays(daysUntilMonday);
                for (var d = startDay; d <= to; d = d.AddDays(7))
                {
                    if (d >= from) dates.Add(d);
                }
                break;

            case ScheduleRhythm.Monthly:
                var current = new DateOnly(from.Year, from.Month, 1);
                if (current < from) current = current.AddMonths(1);
                while (current <= to)
                {
                    dates.Add(current);
                    current = current.AddMonths(1);
                }
                break;

            case ScheduleRhythm.EveryXMonths:
                var interval = schedule.IntervalMonths ?? 3;
                // Start from the 1st of the current month, advance by interval
                var start = new DateOnly(from.Year, from.Month, 1);
                if (start < from) start = start.AddMonths(interval);
                // Align to interval boundaries from Jan of current year
                var yearStart = new DateOnly(from.Year, 1, 1);
                while (yearStart < from)
                    yearStart = yearStart.AddMonths(interval);
                start = yearStart;
                while (start <= to)
                {
                    if (start >= from)
                        dates.Add(start);
                    start = start.AddMonths(interval);
                }
                break;

            case ScheduleRhythm.EveryXWeeks:
                var weekInterval = schedule.IntervalWeeks ?? 2;
                // Find next Monday (same logic as Weekly)
                int daysToMonday;
                if (from.DayOfWeek == DayOfWeek.Monday)
                    daysToMonday = 0;
                else if (from.DayOfWeek == DayOfWeek.Sunday)
                    daysToMonday = 1;
                else
                    daysToMonday = ((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7;

                // Align to week-interval boundaries from start of year
                var weekYearStart = new DateOnly(from.Year, 1, 6); // first Monday of year (approx)
                // Find actual first Monday of year
                while (weekYearStart.DayOfWeek != DayOfWeek.Monday)
                    weekYearStart = weekYearStart.AddDays(-1);
                // Advance by interval weeks until we reach or pass 'from'
                var weekStart2 = weekYearStart;
                while (weekStart2.AddDays(weekInterval * 7) <= from)
                    weekStart2 = weekStart2.AddDays(weekInterval * 7);
                if (weekStart2 < from)
                    weekStart2 = weekStart2.AddDays(weekInterval * 7);

                for (var d = weekStart2; d <= to; d = d.AddDays(weekInterval * 7))
                {
                    if (d >= from)
                        dates.Add(d);
                }
                break;
        }

        return dates;
    }

    private static bool DayMatchesFlag(DayOfWeek day, DaysOfWeek flags) => day switch
    {
        DayOfWeek.Monday => flags.HasFlag(DaysOfWeek.Monday),
        DayOfWeek.Tuesday => flags.HasFlag(DaysOfWeek.Tuesday),
        DayOfWeek.Wednesday => flags.HasFlag(DaysOfWeek.Wednesday),
        DayOfWeek.Thursday => flags.HasFlag(DaysOfWeek.Thursday),
        DayOfWeek.Friday => flags.HasFlag(DaysOfWeek.Friday),
        DayOfWeek.Saturday => flags.HasFlag(DaysOfWeek.Saturday),
        DayOfWeek.Sunday => flags.HasFlag(DaysOfWeek.Sunday),
        _ => false
    };
}
