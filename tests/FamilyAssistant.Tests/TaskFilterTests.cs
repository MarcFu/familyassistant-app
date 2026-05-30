using FamilyAssistant.Data;
using FamilyAssistant.Models;
using FamilyAssistant.Services;

namespace FamilyAssistant.Tests;

/// <summary>
/// Tests for ChoreTaskQueryService — the single source of truth for task filtering.
/// Uses InMemory EF Core to test actual query logic.
/// </summary>
public class TaskFilterTests
{
    #region Helpers

    private static AppDbContext CreateDb() => TestDbFactory.Create();

    private static ChoreTaskQueryService CreateService(AppDbContext db) => new(db);

    private static Chore CreateChore(AppDbContext db, string name = "Test")
    {
        var chore = new Chore { Name = name, CreditsReward = 5, IsActive = true };
        db.Chores.Add(chore);
        db.SaveChanges();
        return chore;
    }

    private static ChoreSchedule CreateSchedule(AppDbContext db, Chore chore, ScheduleRhythm rhythm)
    {
        var schedule = new ChoreSchedule
        {
            ChoreId = chore.Id,
            Chore = chore,
            Rhythm = rhythm,
            IsActive = true,
            TimesPerDay = 1
        };
        db.ChoreSchedules.Add(schedule);
        db.SaveChanges();
        return schedule;
    }

    private static ChoreTask CreateTask(AppDbContext db, Chore chore, ChoreSchedule? schedule,
        DateOnly dueDate, ChoreTaskStatus status = ChoreTaskStatus.Open,
        int? defaultPersonId = null, int? claimedByPersonId = null,
        int? completedByPersonId = null, DateTime? completedAt = null)
    {
        var task = new ChoreTask
        {
            ChoreId = chore.Id,
            Chore = chore,
            ScheduleId = schedule?.Id,
            Schedule = schedule,
            DueDate = dueDate,
            OccurrenceIndex = 1,
            Status = status,
            DefaultPersonId = defaultPersonId,
            ClaimedByPersonId = claimedByPersonId,
            CompletedByPersonId = completedByPersonId,
            CompletedAt = completedAt,
            CreatedAt = DateTime.UtcNow
        };
        db.ChoreTasks.Add(task);
        db.SaveChanges();
        return task;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private static DateOnly Yesterday => Today.AddDays(-1);
    private static DateOnly Tomorrow => Today.AddDays(1);
    private static DateOnly ThisMonday => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
    private static DateOnly ThisSunday => ThisMonday.AddDays(6);

    #endregion

    // ═══════════════════════════════════════════════════════
    // TODAY FILTER (GetTodayTasksAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Today_ShowsDailyOpenTaskDueToday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        var task = CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Single(results);
        Assert.Equal(task.Id, results[0].Id);
    }

    [Fact]
    public async Task Today_ShowsClaimedTaskDueToday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Claimed);

        var results = await svc.GetTodayTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Today_ShowsPendingConfirmationTaskDueToday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.PendingConfirmation);

        var results = await svc.GetTodayTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Today_HidesConfirmedDailyTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow);

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_HidesMissedTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Missed);

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_HidesCancelledTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Cancelled);

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_HidesDailyTaskDueTomorrow()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Tomorrow, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_ShowsWeeklyOpenTaskDueEarlierThisWeek()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Weekly);
        CreateTask(db, chore, schedule, ThisMonday, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Today_HidesWeeklyConfirmedTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Weekly);
        CreateTask(db, chore, schedule, ThisMonday, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddHours(-2));

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_ShowsMonthlyOpenTaskDueThisMonth()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Monthly);
        var firstOfMonth = new DateOnly(Today.Year, Today.Month, 1);
        CreateTask(db, chore, schedule, firstOfMonth, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Today_HidesMonthlyConfirmedTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Monthly);
        var firstOfMonth = new DateOnly(Today.Year, Today.Month, 1);
        CreateTask(db, chore, schedule, firstOfMonth, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddDays(-3));

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_HidesPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_ShowsAdHocTaskDueToday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        CreateTask(db, chore, null, Today, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Today_HidesAdHocTaskDueYesterday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        CreateTask(db, chore, null, Yesterday, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Today_PersonFilter_ShowsOnlyAssignedOrUnassigned()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        // Task assigned to person 1
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open, defaultPersonId: 1);
        // Task assigned to person 2
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open, defaultPersonId: 2);
        // Unassigned task (should appear for both)
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetTodayTasksAsync(personId: 1);

        Assert.Equal(2, results.Count); // person 1's task + unassigned
    }

    // ═══════════════════════════════════════════════════════
    // OPEN FILTER (GetOpenTasksAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Open_ShowsOpenTaskDueToday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetOpenTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Open_ShowsClaimedTaskDueTomorrow()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Tomorrow, ChoreTaskStatus.Claimed);

        var results = await svc.GetOpenTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Open_HidesConfirmedTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow);

        var results = await svc.GetOpenTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Open_HidesPendingConfirmation()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.PendingConfirmation);

        var results = await svc.GetOpenTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Open_HidesPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetOpenTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Open_ShowsWeeklyOpenTaskFromPast()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Weekly);
        CreateTask(db, chore, schedule, ThisMonday, ChoreTaskStatus.Open);

        var results = await svc.GetOpenTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Open_HidesDailyPastDueTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Open);

        var results = await svc.GetOpenTasksAsync();

        Assert.Empty(results);
    }

    // ═══════════════════════════════════════════════════════
    // COMPLETED FILTER (GetCompletedThisWeekAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Completed_ShowsConfirmedTaskCompletedThisWeek()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddHours(-1));

        var results = await svc.GetCompletedThisWeekAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Completed_ShowsPendingConfirmationWithCompletedAt()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.PendingConfirmation,
            completedAt: DateTime.UtcNow.AddHours(-1));

        var results = await svc.GetCompletedThisWeekAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Completed_HidesOpenTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetCompletedThisWeekAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Completed_HidesTaskCompletedBeforeThisWeek()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        var lastWeek = ThisMonday.AddDays(-1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Confirmed,
            completedAt: lastWeek);

        var results = await svc.GetCompletedThisWeekAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Completed_HidesPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddMinutes(-30));

        var results = await svc.GetCompletedThisWeekAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Completed_HidesConfirmedWithoutCompletedAt()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed, completedAt: null);

        var results = await svc.GetCompletedThisWeekAsync();

        Assert.Empty(results);
    }

    // ═══════════════════════════════════════════════════════
    // THIS WEEK FILTER (GetThisWeekTasksAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ThisWeek_ShowsOpenTaskDueThisWeek()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetThisWeekTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task ThisWeek_HidesConfirmedTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow);

        var results = await svc.GetThisWeekTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task ThisWeek_ShowsWeeklyTaskDueEarlierInWeek()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Weekly);
        CreateTask(db, chore, schedule, ThisMonday, ChoreTaskStatus.Open);

        var results = await svc.GetThisWeekTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task ThisWeek_HidesPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetThisWeekTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task ThisWeek_HidesTaskDueNextWeek()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, ThisSunday.AddDays(1), ChoreTaskStatus.Open);

        var results = await svc.GetThisWeekTasksAsync();

        Assert.Empty(results);
    }

    // ═══════════════════════════════════════════════════════
    // OVERDUE FILTER (GetOverdueTasksAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Overdue_ShowsDailyOpenTaskFromYesterday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Open);

        var results = await svc.GetOverdueTasksAsync();

        Assert.Single(results);
    }

    [Fact]
    public async Task Overdue_HidesTaskDueToday()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetOverdueTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Overdue_HidesConfirmedOldTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddHours(-12));

        var results = await svc.GetOverdueTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Overdue_HidesPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Open);

        var results = await svc.GetOverdueTasksAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Overdue_ShowsClaimedOldTask()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Claimed);

        var results = await svc.GetOverdueTasksAsync();

        Assert.Single(results);
    }

    // ═══════════════════════════════════════════════════════
    // PENDING FILTER (GetPendingConfirmationAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Pending_ShowsOnlyPendingConfirmation()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.PendingConfirmation,
            completedAt: DateTime.UtcNow);
        CreateTask(db, chore, schedule, Yesterday, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddDays(-1));

        var results = await svc.GetPendingConfirmationAsync();

        Assert.Single(results);
        Assert.Equal(ChoreTaskStatus.PendingConfirmation, results[0].Status);
    }

    // ═══════════════════════════════════════════════════════
    // PERMANENT FILTER (GetPermanentTasksAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Permanent_ShowsOnlyPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var permSchedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        var dailySchedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, permSchedule, Today, ChoreTaskStatus.Open);
        CreateTask(db, chore, dailySchedule, Today, ChoreTaskStatus.Open);

        var results = await svc.GetPermanentTasksAsync();

        Assert.Single(results);
        Assert.Equal(ScheduleRhythm.Permanent, results[0].Schedule!.Rhythm);
    }

    // ═══════════════════════════════════════════════════════
    // OPEN PERMANENT (GetOpenPermanentTasksAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task OpenPermanent_ShowsOnlyOpenOrClaimed()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow);

        var results = await svc.GetOpenPermanentTasksAsync();

        Assert.Single(results);
        Assert.Equal(ChoreTaskStatus.Open, results[0].Status);
    }

    // ═══════════════════════════════════════════════════════
    // UNCLAIMED COUNT (GetUnclaimedOpenCountAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task UnclaimedCount_CountsOnlyUnassignedOpenTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        // Unassigned open
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open);
        CreateTask(db, chore, schedule, Tomorrow, ChoreTaskStatus.Open);
        // Assigned open (should not count)
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Open, defaultPersonId: 1);
        // Claimed (should not count)
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Claimed, claimedByPersonId: 2);

        var count = await svc.GetUnclaimedOpenCountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task UnclaimedCount_ExcludesPermanentTasks()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var permSchedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        CreateTask(db, chore, permSchedule, Today, ChoreTaskStatus.Open);

        var count = await svc.GetUnclaimedOpenCountAsync();

        Assert.Equal(0, count);
    }

    // ═══════════════════════════════════════════════════════
    // RECENT COMPLETED FOR PERSON (GetRecentCompletedForPersonAsync)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task RecentCompleted_ReturnsOnlyForSpecifiedPerson()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedByPersonId: 1, completedAt: DateTime.UtcNow.AddHours(-1));
        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedByPersonId: 2, completedAt: DateTime.UtcNow.AddHours(-2));

        var results = await svc.GetRecentCompletedForPersonAsync(1);

        Assert.Single(results);
    }

    [Fact]
    public async Task RecentCompleted_RespectsMaxCount()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        for (int i = 0; i < 10; i++)
        {
            CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
                completedByPersonId: 1, completedAt: DateTime.UtcNow.AddHours(-i));
        }

        var results = await svc.GetRecentCompletedForPersonAsync(1, maxCount: 3);

        Assert.Equal(3, results.Count);
    }

    // ═══════════════════════════════════════════════════════
    // CROSS-CUTTING: The key UX scenario
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task KeyScenario_WeeklyTaskDisappearsFromTodayAfterCompletion()
    {
        // THE key scenario: Louanne completes a weekly task → vanishes from Today, visible in Completed
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db, "Katzentoilette");
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Weekly);

        CreateTask(db, chore, schedule, ThisMonday, ChoreTaskStatus.Confirmed,
            completedByPersonId: 1, completedAt: DateTime.UtcNow);

        var todayResults = await svc.GetTodayTasksAsync();
        var completedResults = await svc.GetCompletedThisWeekAsync();

        Assert.Empty(todayResults);
        Assert.Single(completedResults);
    }

    [Fact]
    public async Task KeyScenario_DailyTaskDisappearsFromTodayAfterCompletion()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db, "Zimmer aufräumen");
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);

        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.Confirmed,
            completedByPersonId: 1, completedAt: DateTime.UtcNow);

        var todayResults = await svc.GetTodayTasksAsync();
        var completedResults = await svc.GetCompletedThisWeekAsync();

        Assert.Empty(todayResults);
        Assert.Single(completedResults);
    }

    [Fact]
    public async Task KeyScenario_PendingConfirmationStaysInTodayView()
    {
        // Child marks task done → PendingConfirmation → still visible in Today (needs parent action)
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db, "Spülmaschine");
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);

        CreateTask(db, chore, schedule, Today, ChoreTaskStatus.PendingConfirmation,
            completedByPersonId: 1, completedAt: DateTime.UtcNow);

        var todayResults = await svc.GetTodayTasksAsync();

        Assert.Single(todayResults);
    }

    [Fact]
    public async Task MixedScenario_AllFiltersReturnCorrectSubsets()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var chore = CreateChore(db, "Gemischt");
        var dailySchedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        var weeklySchedule = CreateSchedule(db, chore, ScheduleRhythm.Weekly);
        var permSchedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);

        // t1: Open daily due today
        var t1 = CreateTask(db, chore, dailySchedule, Today, ChoreTaskStatus.Open);
        // t2: Claimed weekly due this week
        var t2 = CreateTask(db, chore, weeklySchedule, ThisMonday, ChoreTaskStatus.Claimed);
        // t3: Confirmed daily from today
        var t3 = CreateTask(db, chore, dailySchedule, Today, ChoreTaskStatus.Confirmed,
            completedAt: DateTime.UtcNow.AddHours(-1));
        // t4: Open daily from yesterday (overdue)
        var t4 = CreateTask(db, chore, dailySchedule, Yesterday, ChoreTaskStatus.Open);
        // t5: Pending confirmation
        var t5 = CreateTask(db, chore, dailySchedule, Today, ChoreTaskStatus.PendingConfirmation,
            completedAt: DateTime.UtcNow.AddMinutes(-30));
        // t6: Permanent open
        var t6 = CreateTask(db, chore, permSchedule, Today, ChoreTaskStatus.Open);

        // Today: t1, t2, t5
        var today = (await svc.GetTodayTasksAsync()).Select(t => t.Id).ToList();
        Assert.Contains(t1.Id, today);
        Assert.Contains(t2.Id, today);
        Assert.Contains(t5.Id, today);
        Assert.DoesNotContain(t3.Id, today);
        Assert.DoesNotContain(t4.Id, today);
        Assert.DoesNotContain(t6.Id, today);

        // Open: t1, t2
        var open = (await svc.GetOpenTasksAsync()).Select(t => t.Id).ToList();
        Assert.Contains(t1.Id, open);
        Assert.Contains(t2.Id, open);
        Assert.DoesNotContain(t3.Id, open);
        Assert.DoesNotContain(t5.Id, open);
        Assert.DoesNotContain(t6.Id, open);

        // Completed: t3, t5
        var completed = (await svc.GetCompletedThisWeekAsync()).Select(t => t.Id).ToList();
        Assert.Contains(t3.Id, completed);
        Assert.Contains(t5.Id, completed);
        Assert.DoesNotContain(t1.Id, completed);

        // Overdue: t4
        var overdue = (await svc.GetOverdueTasksAsync()).Select(t => t.Id).ToList();
        Assert.Contains(t4.Id, overdue);
        Assert.DoesNotContain(t1.Id, overdue);

        // Pending: t5
        var pending = (await svc.GetPendingConfirmationAsync()).Select(t => t.Id).ToList();
        Assert.Contains(t5.Id, pending);
        Assert.Single(pending);

        // Permanent: t6
        var permanent = (await svc.GetPermanentTasksAsync()).Select(t => t.Id).ToList();
        Assert.Contains(t6.Id, permanent);
        Assert.Single(permanent);
    }
}
