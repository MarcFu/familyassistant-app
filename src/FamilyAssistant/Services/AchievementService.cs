using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

/// <summary>
/// Evaluates achievement unlock state for a person based on task history.
/// </summary>
public sealed class AchievementService : IAchievementService
{
    private readonly AppDbContext _db;

    public AchievementService(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<AchievementDefinition> Definitions { get; } = BuildDefinitions();

    public async Task<AchievementEvaluation> EvaluateAsync(int personId)
    {
        var unlocked = new HashSet<string>();
        var facts = new Dictionary<string, string>();

        var persons = await _db.Persons.AsNoTracking().ToListAsync();
        var tasks = await _db.ChoreTasks
            .AsNoTracking()
            .Include(t => t.Chore)
            .Include(t => t.Comments)
                .ThenInclude(c => c.Attachments)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var completedByPerson = tasks
            .Where(t => t.CompletedAt is not null && (t.CompletedByPersonId == personId || t.ClaimedByPersonId == personId))
            .ToList();
        var confirmedByPerson = completedByPerson
            .Where(t => t.Status == ChoreTaskStatus.Confirmed)
            .ToList();
        var eventTasks = completedByPerson.Where(IsTriggeredTask).ToList();
        var allConfirmed = tasks.Where(t => t.Status == ChoreTaskStatus.Confirmed).ToList();

        var completedCount = completedByPerson.Count;
        var streak = GetLongestStreak(completedByPerson);
        var currentStreak = GetCurrentStreak(completedByPerson);
        var eventCount = eventTasks.Count;
        var bestChoreCount = completedByPerson.GroupBy(t => t.ChoreId).Select(g => g.Count()).DefaultIfEmpty(0).Max();
        var uniqueChores = completedByPerson.Select(t => t.ChoreId).Distinct().Count();
        var activeChores = await _db.Chores.AsNoTracking().CountAsync(c => c.IsActive);
        var photoCount = completedByPerson.Count(t => t.Comments.Any(c => c.Attachments.Count > 0));
        var commentCount = completedByPerson.Count(t => t.Comments.Any(c => c.PersonId == personId && !string.IsNullOrWhiteSpace(c.Text)));
        var dialogueCount = completedByPerson.Count(t => t.Comments.Count >= 2);
        var confirmationCount = tasks.Count(t => t.ConfirmedByPersonId == personId);
        var helperCount = completedByPerson.Count(t => t.CompletedByPersonId == personId && t.DefaultPersonId is not null && t.DefaultPersonId != personId);
        var fastFinishCount = completedByPerson.Count(t => !IsTriggeredTask(t) && t.ScheduleId is null && t.CompletedAt is not null && t.CompletedAt.Value - t.CreatedAt <= TimeSpan.FromMinutes(30));
        var roadrunnerSeconds = eventTasks
            .Where(t => t.CompletedAt is not null)
            .Select(t => (t.CompletedAt!.Value - t.CreatedAt).TotalSeconds)
            .Where(s => s >= 0)
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();

        void Unlock(string key, bool isUnlocked, string fact)
        {
            if (isUnlocked) unlocked.Add(key);
            facts[key] = fact;
        }

        Unlock("first_task", completedCount >= 1, $"{completedCount} erledigte Aufgaben.");
        Unlock("first_chore", completedByPerson.Any(t => t.ScheduleId is not null), $"{completedByPerson.Count(t => t.ScheduleId is not null)} wiederkehrende Aufgaben.");
        Unlock("first_photo", photoCount >= 1, $"{photoCount} Aufgaben mit Foto.");
        Unlock("first_event_task", eventCount >= 1, $"{eventCount} Trigger-Aufgaben.");
        Unlock("daily_clean_sweep", HasDailyCleanSweep(tasks, personId, today), "Alle eigenen Aufgaben eines Tages erledigt.");
        Unlock("perfect_week", HasPerfectWeek(tasks, personId), "Woche ohne verpasste Aufgaben.");
        Unlock("no_reminder_needed", completedByPerson.Any(t => t.CompletedAt is not null && t.CompletedAt.Value.ToLocalTime().Hour < 12), "Mindestens eine Aufgabe vor Mittag.");
        Unlock("comeback", HasComeback(completedByPerson), "Nach Pause zurückgekehrt.");
        Unlock("streak_3", streak >= 3, $"Beste Serie: {streak} Tage.");
        Unlock("streak_7", streak >= 7, $"Beste Serie: {streak} Tage.");
        Unlock("streak_30", streak >= 30, $"Beste Serie: {streak} Tage.");
        Unlock("streak_100", streak >= 100, $"Beste Serie: {streak} Tage.");
        Unlock("task_count_10", completedCount >= 10, $"{completedCount}/10.");
        Unlock("task_count_50", completedCount >= 50, $"{completedCount}/50.");
        Unlock("task_count_100", completedCount >= 100, $"{completedCount}/100.");
        Unlock("task_count_500", completedCount >= 500, $"{completedCount}/500.");
        Unlock("chore_master_10", bestChoreCount >= 10, $"Beste Chore: {bestChoreCount}/10.");
        Unlock("chore_master_50", bestChoreCount >= 50, $"Beste Chore: {bestChoreCount}/50.");
        Unlock("chore_master_100", bestChoreCount >= 100, $"Beste Chore: {bestChoreCount}/100.");
        Unlock("early_bird", completedByPerson.Any(t => t.CompletedAt is not null && t.CompletedAt.Value.ToLocalTime().Hour < 8), "Aufgabe vor 8 Uhr.");
        Unlock("night_owl", completedByPerson.Any(t => t.CompletedAt is not null && t.CompletedAt.Value.ToLocalTime().Hour >= 22), "Aufgabe nach 22 Uhr.");
        Unlock("weekend_warrior", completedByPerson.Any(t => t.CompletedAt is not null && t.CompletedAt.Value.ToLocalTime().DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday), "Wochenend-Aufgabe erledigt.");
        Unlock("last_minute_hero", completedByPerson.Any(IsLastMinute), "Aufgabe kurz vor Deadline.");
        Unlock("fast_finish", fastFinishCount >= 1, $"{fastFinishCount} schnelle Erledigung(en).");
        Unlock("roadrunner_10", roadrunnerSeconds <= 600, GetRoadrunnerFact(roadrunnerSeconds));
        Unlock("roadrunner_5", roadrunnerSeconds <= 300, GetRoadrunnerFact(roadrunnerSeconds));
        Unlock("roadrunner_1", roadrunnerSeconds <= 60, GetRoadrunnerFact(roadrunnerSeconds));
        Unlock("automation_whisperer", eventCount >= 10, $"{eventCount}/10 Trigger-Aufgaben.");
        Unlock("sensor_sprinter", eventTasks.Select(t => t.OccurrenceLabel).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().Count() >= 5, $"{eventTasks.Select(t => t.OccurrenceLabel).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().Count()}/5 Quellen.");
        Unlock("energy_saver", completedByPerson.Any(t => HasAnyKeyword(t.Chore.Name, "strom", "energie", "licht", "standby", "heizung")), "Energie-Keyword erkannt.");
        Unlock("weather_ready", completedByPerson.Any(t => HasAnyKeyword(t.Chore.Name, "regen", "wetter", "pflanze", "garten", "fenster")), "Wetter-Keyword erkannt.");
        Unlock("helper", helperCount >= 1, $"{helperCount} Aufgaben für andere.");
        Unlock("team_day", HasTeamDay(tasks, persons), "Alle aktiven Personen an einem Tag aktiv.");
        Unlock("family_clean_sweep", HasFamilyCleanSweep(tasks, today), "Alle Familien-Aufgaben eines Tages erledigt.");
        Unlock("category_boss", false, "Keine Kategorie-Daten im Modell.");
        Unlock("category_master_25", false, "Keine Kategorie-Daten im Modell.");
        Unlock("category_master_100", false, "Keine Kategorie-Daten im Modell.");
        Unlock("photo_journalist", photoCount >= 10, $"{photoCount}/10 Foto-Aufgaben.");
        Unlock("commentator", commentCount >= 25, $"{commentCount}/25 kommentierte Aufgaben.");
        Unlock("quiet_hero", HasQuietHero(tasks, personId), "Allein an einem Tag aktiv.");
        Unlock("midnight_rescue", completedByPerson.Any(t => t.CompletedAt is not null && t.CompletedAt.Value.ToLocalTime().Hour is >= 0 and < 5), "Aufgabe nach Mitternacht.");
        Unlock("same_chore_weekly_4", HasWeeklyChoreRun(completedByPerson, 4), "Gleiche Chore 4+ Wochen.");
        Unlock("same_chore_weekly_12", HasWeeklyChoreRun(completedByPerson, 12), "Gleiche Chore 12+ Wochen.");
        Unlock("all_rounder", uniqueChores >= 5, $"{uniqueChores}/5 verschiedene Chores.");
        Unlock("category_explorer", activeChores > 0 && uniqueChores >= activeChores, $"{uniqueChores}/{activeChores} aktive Chores.");
        Unlock("fair_share", HasFairShare(tasks, personId), "Anteil nahe am Familiendurchschnitt.");
        Unlock("household_marathon", allConfirmed.Count >= 100, $"Familie: {allConfirmed.Count}/100.");
        Unlock("morning_routine", HasTasksInDay(completedByPerson, t => t.CompletedAt is not null && t.CompletedAt.Value.ToLocalTime().Hour < 10, 3), "3+ Morgen-Aufgaben an einem Tag.");
        Unlock("reset_day", HasTasksInDay(completedByPerson, _ => true, 5), "5+ Aufgaben an einem Tag.");
        Unlock("confirmation_pro", confirmationCount >= 10, $"{confirmationCount}/10 Bestätigungen.");
        Unlock("pin_collector", false, "Pinned-Tasks nicht persistiert.");
        Unlock("dialogue", dialogueCount >= 5, $"{dialogueCount}/5 Aufgaben mit Dialog.");
        Unlock("deadline_dodger", HasTasksInDay(completedByPerson, IsLastMinute, 3), "3+ Last-Minute an einem Tag.");
        Unlock("automation_chain", HasTasksInDay(eventTasks, _ => true, 3), "3+ Trigger-Aufgaben an einem Tag.");
        Unlock("family_marathon_500", allConfirmed.Count >= 500, $"Familie: {allConfirmed.Count}/500.");

        var stats = new ProfileStats(
            TotalCompleted: completedCount,
            CurrentStreak: currentStreak,
            LongestStreak: streak,
            UnlockedBadges: unlocked.Count,
            TotalBadges: Definitions.Count);

        return new AchievementEvaluation(unlocked, facts, stats);
    }

    // ─── Helper methods ─────────────────────────────────────────────────────────

    private static bool IsTriggeredTask(ChoreTask task)
    {
        return task.ScheduleId is null
            && task.OccurrenceLabel?.StartsWith("Trigger:", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static int GetLongestStreak(List<ChoreTask> tasks)
    {
        var days = GetCompletionDays(tasks);
        return CalculateBestStreak(days);
    }

    private static int GetCurrentStreak(List<ChoreTask> tasks)
    {
        var days = GetCompletionDays(tasks);
        if (days.Count == 0) return 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var current = 0;

        // Start from today or yesterday (allow current day to not be completed yet)
        var checkDate = days.Contains(today) ? today : today.AddDays(-1);
        if (!days.Contains(checkDate)) return 0;

        while (days.Contains(checkDate))
        {
            current++;
            checkDate = checkDate.AddDays(-1);
        }

        return current;
    }

    private static List<DateOnly> GetCompletionDays(List<ChoreTask> tasks)
    {
        return tasks
            .Where(t => t.CompletedAt is not null)
            .Select(t => DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()))
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }

    private static int CalculateBestStreak(List<DateOnly> days)
    {
        var best = 0;
        var current = 0;
        DateOnly? previous = null;

        foreach (var day in days)
        {
            current = previous is not null && day == previous.Value.AddDays(1) ? current + 1 : 1;
            best = Math.Max(best, current);
            previous = day;
        }

        return best;
    }

    private static bool HasComeback(List<ChoreTask> tasks)
    {
        var days = GetCompletionDays(tasks);
        return days.Zip(days.Skip(1), (a, b) => b.DayNumber - a.DayNumber).Any(gap => gap >= 7);
    }

    private static bool IsLastMinute(ChoreTask task)
    {
        if (task.CompletedAt is null) return false;
        var completed = task.CompletedAt.Value.ToLocalTime();
        return DateOnly.FromDateTime(completed) == task.DueDate && completed.Hour >= 18;
    }

    private static string GetRoadrunnerFact(double seconds)
    {
        return double.IsPositiveInfinity(seconds)
            ? "Noch keine Trigger-Aufgabe."
            : $"Schnellste Reaktion: {TimeSpan.FromSeconds(seconds):mm\\:ss}.";
    }

    private static bool HasAnyKeyword(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasTeamDay(List<ChoreTask> tasks, List<Person> persons)
    {
        var activePersonIds = persons
            .Where(p => p.Role != PersonRole.Guest && !p.IsPaused)
            .Select(p => p.Id)
            .ToHashSet();

        return activePersonIds.Count > 0 && tasks
            .Where(t => t.CompletedAt is not null && t.CompletedByPersonId is not null)
            .GroupBy(t => DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()))
            .Any(g => activePersonIds.All(id => g.Any(t => t.CompletedByPersonId == id || t.ClaimedByPersonId == id)));
    }

    private static bool HasFamilyCleanSweep(List<ChoreTask> tasks, DateOnly today)
    {
        var todayTasks = tasks.Where(t => t.DueDate == today).ToList();
        return todayTasks.Any(t => t.CompletedAt is not null)
            && todayTasks.All(t => t.Status is ChoreTaskStatus.Confirmed or ChoreTaskStatus.Missed or ChoreTaskStatus.Cancelled);
    }

    private static bool HasDailyCleanSweep(List<ChoreTask> tasks, int personId, DateOnly today)
    {
        var visibleToday = tasks.Where(t => t.DueDate == today && IsRelevantToPerson(t, personId)).ToList();
        return visibleToday.Any(t => t.CompletedAt is not null)
            && visibleToday.All(t => t.Status is ChoreTaskStatus.Confirmed or ChoreTaskStatus.Missed or ChoreTaskStatus.Cancelled);
    }

    private static bool HasPerfectWeek(List<ChoreTask> tasks, int personId)
    {
        var start = WeekStart(DateOnly.FromDateTime(DateTime.Today));
        var end = start.AddDays(7);
        var weekTasks = tasks.Where(t => t.DueDate >= start && t.DueDate < end && IsRelevantToPerson(t, personId)).ToList();
        return weekTasks.Any(t => t.Status == ChoreTaskStatus.Confirmed)
            && weekTasks.All(t => t.Status is not ChoreTaskStatus.Missed and not ChoreTaskStatus.Cancelled);
    }

    private static bool HasQuietHero(List<ChoreTask> tasks, int personId)
    {
        return tasks
            .Where(t => t.CompletedAt is not null && (t.CompletedByPersonId is not null || t.ClaimedByPersonId is not null))
            .GroupBy(t => DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()))
            .Any(g => g.Any(t => t.CompletedByPersonId == personId || t.ClaimedByPersonId == personId)
                && g.All(t => t.CompletedByPersonId == personId || t.ClaimedByPersonId == personId));
    }

    private static bool HasWeeklyChoreRun(List<ChoreTask> tasks, int requiredWeeks)
    {
        return tasks
            .Where(t => t.CompletedAt is not null)
            .GroupBy(t => t.ChoreId)
            .Any(g => g.Select(t => WeekStart(DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()))).Distinct().Count() >= requiredWeeks);
    }

    private static bool HasFairShare(List<ChoreTask> tasks, int personId)
    {
        var start = WeekStart(DateOnly.FromDateTime(DateTime.Today));
        var end = start.AddDays(7);
        var counts = tasks
            .Where(t => t.CompletedAt is not null)
            .Where(t => DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()) >= start && DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()) < end)
            .GroupBy(t => t.CompletedByPersonId ?? t.ClaimedByPersonId)
            .Where(g => g.Key is not null)
            .ToDictionary(g => g.Key!.Value, g => g.Count());

        if (!counts.TryGetValue(personId, out var ownCount) || counts.Count < 2)
            return false;

        var average = counts.Values.Average();
        return ownCount >= Math.Max(1, average * 0.8);
    }

    private static bool HasTasksInDay(List<ChoreTask> tasks, Func<ChoreTask, bool> predicate, int requiredCount)
    {
        return tasks
            .Where(t => t.CompletedAt is not null && predicate(t))
            .GroupBy(t => DateOnly.FromDateTime(t.CompletedAt!.Value.ToLocalTime()))
            .Any(g => g.Count() >= requiredCount);
    }

    private static DateOnly WeekStart(DateOnly date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.AddDays(-offset);
    }

    private static bool IsRelevantToPerson(ChoreTask task, int personId)
    {
        return task.CompletedByPersonId == personId
            || task.ClaimedByPersonId == personId
            || task.DefaultPersonId == personId
            || (task.DefaultPersonId is null && task.ClaimedByPersonId is null);
    }

    // ─── Definitions ────────────────────────────────────────────────────────────

    private static List<AchievementDefinition> BuildDefinitions() =>
    [
        new("first_task", "Der Anfang", "Einstieg", "Erste Aufgabe erledigt", "Base", "CheckCircle"),
        new("first_chore", "Routine gestartet", "Einstieg", "Erste wiederkehrende Chore erledigt", "Base", "TaskAlt"),
        new("first_photo", "Beweisfoto", "Einstieg", "Erste Aufgabe mit Foto-Kommentar abgeschlossen", "Base", "CameraAlt"),
        new("first_event_task", "Smart Start", "Home Assistant", "Erste eventgetriebene Aufgabe erledigt", "Base", "Sensors"),
        new("daily_clean_sweep", "Clean Sweep", "Tagesleistung", "Alle offenen Aufgaben eines Tages erledigt", "Base", "CheckCircle"),
        new("perfect_week", "Perfekte Woche", "Zuverlässigkeit", "Eine Woche ohne verpasste Aufgaben", "Base", "CalendarMonth"),
        new("no_reminder_needed", "Ohne Erinnerung", "Zuverlässigkeit", "Aufgabe erledigt, bevor Erinnerung nötig", "Base", "Notifications"),
        new("comeback", "Comeback", "Motivation", "Nach längerer Pause wieder aktiv", "Base", "RestartAlt"),
        new("streak_3", "Auf Kurs", "Streak", "3 Tage am Stück", "Bronze", "AutoAwesome"),
        new("streak_7", "Wochenserie", "Streak", "7 Tage am Stück", "Silber", "AutoAwesome"),
        new("streak_30", "Monatsmodus", "Streak", "30 Tage am Stück", "Gold", "AutoAwesome"),
        new("streak_100", "Unaufhaltsam", "Streak", "100 Tage am Stück", "Legendär", "AutoAwesome"),
        new("task_count_10", "Angepackt", "Menge", "10 Aufgaben erledigt", "Bronze", "TaskAlt"),
        new("task_count_50", "Haushaltsprofi", "Menge", "50 Aufgaben erledigt", "Silber", "TaskAlt"),
        new("task_count_100", "Maschinenraum", "Menge", "100 Aufgaben erledigt", "Gold", "TaskAlt"),
        new("task_count_500", "Household Legend", "Menge", "500 Aufgaben erledigt", "Legendär", "TaskAlt"),
        new("chore_master_10", "Wiederholungstäter", "Chore-Mastery", "Dieselbe Chore 10x erledigt", "Bronze", "CleaningServices"),
        new("chore_master_50", "Fachkraft", "Chore-Mastery", "Dieselbe Chore 50x erledigt", "Silber", "CleaningServices"),
        new("chore_master_100", "Meisterschaft", "Chore-Mastery", "Dieselbe Chore 100x erledigt", "Gold", "CleaningServices"),
        new("early_bird", "Early Bird", "Tageszeit", "Aufgabe vor 8 Uhr erledigt", "Base", "PlayArrow"),
        new("night_owl", "Night Owl", "Tageszeit", "Aufgabe nach 22 Uhr erledigt", "Base", "Settings"),
        new("weekend_warrior", "Weekend Warrior", "Tageszeit", "Wochenend-Aufgabe erledigt", "Base", "CalendarMonth"),
        new("last_minute_hero", "Last-Minute Hero", "Timing", "Aufgabe kurz vor Deadline erledigt", "Base", "HourglassTop"),
        new("fast_finish", "Zackig", "Timing", "Ad-hoc-Aufgabe in unter 30 Min erledigt", "Base", "Sync"),
        new("roadrunner_10", "Roadrunner", "Event-Reaktion", "Trigger-Aufgabe in 10 Min erledigt", "Bronze", "DirectionsWalk"),
        new("roadrunner_5", "Roadrunner Plus", "Event-Reaktion", "Trigger-Aufgabe in 5 Min erledigt", "Silber", "DirectionsWalk"),
        new("roadrunner_1", "Meep Meep!", "Event-Reaktion", "Trigger-Aufgabe in 60 Sek erledigt", "Gold", "DirectionsWalk"),
        new("automation_whisperer", "Automation Whisperer", "Home Assistant", "10 Trigger-Aufgaben erledigt", "Base", "Sensors"),
        new("sensor_sprinter", "Sensor Sprinter", "Home Assistant", "5 verschiedene Trigger-Quellen", "Base", "Sensors"),
        new("energy_saver", "Energy Saver", "Home Assistant", "Energie-Aufgabe erledigt", "Base", "Cable"),
        new("weather_ready", "Weather Ready", "Home Assistant", "Wetter-Aufgabe erledigt", "Base", "Home"),
        new("helper", "Helper", "Teamplay", "Aufgabe für jemand anderen erledigt", "Base", "PersonAdd"),
        new("team_day", "Team Day", "Familie", "Alle Familienmitglieder an einem Tag aktiv", "Base", "People"),
        new("family_clean_sweep", "Family Clean Sweep", "Familie", "Familie erledigt alle Aufgaben eines Tages", "Base", "People"),
        new("category_boss", "Mini Boss", "Kategorie", "Alle Aufgaben einer Kategorie erledigt", "Base", "FilterAlt"),
        new("category_master_25", "Kategorien-Profi", "Kategorie", "25 Aufgaben in einer Kategorie", "Silber", "FilterAlt"),
        new("category_master_100", "Kategorien-Legende", "Kategorie", "100 Aufgaben in einer Kategorie", "Gold", "FilterAlt"),
        new("photo_journalist", "Foto-Journalist", "Kommentare", "10 Aufgaben mit Foto", "Base", "PhotoLibrary"),
        new("commentator", "Kommentator", "Kommentare", "25 Aufgaben mit Text-Kommentar", "Base", "ChatBubbleOutline"),
        new("quiet_hero", "Quiet Hero", "Hidden", "Allein an einem Tag aktiv", "Hidden", "AutoAwesome", true),
        new("midnight_rescue", "Midnight Rescue", "Hidden", "Aufgabe nach Mitternacht erledigt", "Hidden", "AutoAwesome", true),
        new("same_chore_weekly_4", "Konsequent", "Konsistenz", "Gleiche Chore 4 Wochen zuverlässig", "Base", "CalendarMonth"),
        new("same_chore_weekly_12", "Verlässlich", "Konsistenz", "Gleiche Chore 12 Wochen zuverlässig", "Base", "CalendarMonth"),
        new("all_rounder", "Allrounder", "Vielfalt", "5 verschiedene Chores erledigt", "Base", "Rule"),
        new("category_explorer", "Explorer", "Vielfalt", "Jede aktive Chore mindestens einmal", "Base", "Search"),
        new("fair_share", "Fair Share", "Familie", "Eigener Anteil nahe am Durchschnitt", "Base", "People"),
        new("household_marathon", "Household Marathon", "Familie", "Familie: 100 Aufgaben gemeinsam", "Base", "People"),
        new("morning_routine", "Morgenroutine", "Tageszeit", "3 Morgen-Aufgaben an einem Tag", "Base", "PlayArrow"),
        new("reset_day", "Reset Day", "Tagesleistung", "Rückstand an einem Tag aufgeholt", "Base", "RestartAlt"),
        new("confirmation_pro", "Bestätigt", "Teamplay", "10 Aufgaben anderer bestätigt", "Base", "ThumbUp"),
        new("pin_collector", "Angepinnt", "Vielfalt", "Erste angepinnte Aufgabe erledigt", "Base", "PushPin"),
        new("dialogue", "Gute Absprache", "Kommentare", "5 Aufgaben mit Kommentarverlauf", "Base", "ChatBubbleOutline"),
        new("deadline_dodger", "Deadline Dodger", "Timing", "3 Last-Minute-Aufgaben an einem Tag", "Silber", "HourglassTop"),
        new("automation_chain", "Kettenreaktion", "Home Assistant", "3 Trigger-Aufgaben an einem Tag", "Silber", "Sensors"),
        new("family_marathon_500", "Familien-Legende", "Familie", "Familie: 500 Aufgaben gemeinsam", "Legendär", "People"),
    ];
}
