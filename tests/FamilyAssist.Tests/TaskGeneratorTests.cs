using FamilyAssist.Data;
using FamilyAssist.Models;
using FamilyAssist.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilyAssist.Tests;

public class TaskGeneratorTests
{
    private static TaskGenerator CreateGenerator(AppDbContext db)
        => new(db, NullLogger<TaskGenerator>.Instance);

    private static Chore CreateChore(AppDbContext db, string name = "Test Chore", int credits = 5)
    {
        var chore = new Chore { Name = name, CreditsReward = credits, IsActive = true };
        db.Chores.Add(chore);
        db.SaveChanges();
        return chore;
    }

    private static ChoreSchedule CreateSchedule(AppDbContext db, Chore chore, ScheduleRhythm rhythm,
        int timesPerDay = 1, DaysOfWeek? specificDays = null, int? timesPerWeek = null,
        int? intervalMonths = null, int? intervalWeeks = null)
    {
        var schedule = new ChoreSchedule
        {
            ChoreId = chore.Id,
            Chore = chore,
            Rhythm = rhythm,
            TimesPerDay = timesPerDay,
            SpecificDays = specificDays,
            TimesPerWeek = timesPerWeek,
            IntervalMonths = intervalMonths,
            IntervalWeeks = intervalWeeks,
            IsActive = true
        };
        db.ChoreSchedules.Add(schedule);
        db.SaveChanges();
        return schedule;
    }

    [Fact]
    public async Task RunAsync_DailySchedule_CreatesTasksFor4Weeks()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        CreateSchedule(db, chore, ScheduleRhythm.Daily);
        var generator = CreateGenerator(db);

        // Act
        var created = await generator.RunAsync();

        // Assert
        Assert.True(created >= 28, $"Expected at least 28 tasks for 4 weeks, got {created}");
        Assert.All(db.ChoreTasks.ToList(), t =>
        {
            Assert.Equal(ChoreTaskStatus.Open, t.Status);
            Assert.Equal(chore.Id, t.ChoreId);
        });
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_NoDuplicates()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        CreateSchedule(db, chore, ScheduleRhythm.Daily);
        var generator = CreateGenerator(db);

        // Act
        var first = await generator.RunAsync();
        var second = await generator.RunAsync();

        // Assert
        Assert.True(first > 0);
        Assert.Equal(0, second); // No new tasks on second run
    }

    [Fact]
    public async Task RunAsync_SpecificDays_OnlyCreatesMWF()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        var days = DaysOfWeek.Monday | DaysOfWeek.Wednesday | DaysOfWeek.Friday;
        CreateSchedule(db, chore, ScheduleRhythm.SpecificDays, specificDays: days);
        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        var tasks = db.ChoreTasks.ToList();
        Assert.All(tasks, t =>
        {
            var dow = t.DueDate.DayOfWeek;
            Assert.True(
                dow == DayOfWeek.Monday || dow == DayOfWeek.Wednesday || dow == DayOfWeek.Friday,
                $"Task has unexpected day: {dow} on {t.DueDate}");
        });
    }

    [Fact]
    public async Task RunAsync_MultiPerDay_CreatesMultipleOccurrences()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        CreateSchedule(db, chore, ScheduleRhythm.Daily, timesPerDay: 2);
        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        var tasks = db.ChoreTasks.ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayTasks = tasks.Where(t => t.DueDate == today).ToList();
        Assert.Equal(2, todayTasks.Count);
        Assert.Contains(todayTasks, t => t.OccurrenceIndex == 1);
        Assert.Contains(todayTasks, t => t.OccurrenceIndex == 2);
    }

    [Fact]
    public async Task RunAsync_PermanentSchedule_CreatesExactlyOneTask()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        var tasks = db.ChoreTasks.ToList();
        Assert.Single(tasks);
        Assert.Equal(ChoreTaskStatus.Open, tasks[0].Status);
    }

    [Fact]
    public async Task RunAsync_PermanentSchedule_DoesNotCreateWhenOpenExists()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        var generator = CreateGenerator(db);

        // Pre-create an open task
        await generator.RunAsync();
        var firstRun = db.ChoreTasks.Count();

        // Act
        await generator.RunAsync();

        // Assert - no new task
        Assert.Equal(firstRun, db.ChoreTasks.Count());
    }

    [Fact]
    public async Task RunAsync_WeeklySchedule_CreatesOnePerWeek()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        CreateSchedule(db, chore, ScheduleRhythm.Weekly);
        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        var tasks = db.ChoreTasks.ToList();
        Assert.InRange(tasks.Count, 3, 5); // 4 weeks ≈ 4-5 Mondays depending on start day
        // All tasks should be on Monday
        Assert.All(tasks, t => Assert.Equal(DayOfWeek.Monday, t.DueDate.DayOfWeek));
    }

    [Fact]
    public async Task RunAsync_MonthlySchedule_CreatesOnePerMonth()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        CreateSchedule(db, chore, ScheduleRhythm.Monthly);
        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        var tasks = db.ChoreTasks.ToList();
        Assert.InRange(tasks.Count, 1, 2); // 4 weeks = 1-2 months
        // All tasks should be on the 1st
        Assert.All(tasks, t => Assert.Equal(1, t.DueDate.Day));
    }

    [Fact]
    public async Task RunAsync_InactiveSchedule_SkipsGeneration()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        schedule.IsActive = false;
        db.SaveChanges();
        var generator = CreateGenerator(db);

        // Act
        var created = await generator.RunAsync();

        // Assert
        Assert.Equal(0, created);
    }

    [Fact]
    public async Task RunAsync_InactiveChore_SkipsGeneration()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        chore.IsActive = false;
        db.SaveChanges();
        CreateSchedule(db, chore, ScheduleRhythm.Daily);
        var generator = CreateGenerator(db);

        // Act
        var created = await generator.RunAsync();

        // Assert
        Assert.Equal(0, created);
    }

    [Fact]
    public async Task RunAsync_PausedPerson_ClearsDefaultPersonId()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var person = new Person { Name = "Test Kid", HaEntityId = "person.kid", Role = PersonRole.Child, IsPaused = true };
        db.Persons.Add(person);
        db.SaveChanges();

        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);
        schedule.DefaultPersonId = person.Id;
        schedule.DefaultPerson = person;
        db.SaveChanges();
        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        var tasks = db.ChoreTasks.ToList();
        Assert.All(tasks, t => Assert.Null(t.DefaultPersonId));
    }

    [Fact]
    public async Task RunAsync_MissedTasks_MarksOverdueAsExpired()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db, credits: 10);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Daily);

        // Create an overdue task (yesterday) that's still open
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdueTask = new ChoreTask
        {
            ChoreId = chore.Id,
            ScheduleId = schedule.Id,
            DueDate = yesterday,
            OccurrenceIndex = 1,
            Status = ChoreTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        db.ChoreTasks.Add(overdueTask);

        // Also create today's task (newer task exists)
        var todayTask = new ChoreTask
        {
            ChoreId = chore.Id,
            ScheduleId = schedule.Id,
            DueDate = today,
            OccurrenceIndex = 1,
            Status = ChoreTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        db.ChoreTasks.Add(todayTask);
        db.SaveChanges();

        var generator = CreateGenerator(db);

        // Act
        await generator.RunAsync();

        // Assert
        await db.Entry(overdueTask).ReloadAsync();
        Assert.Equal(ChoreTaskStatus.Missed, overdueTask.Status);
    }

    [Fact]
    public async Task RespawnPermanentTaskAsync_CreatesNewTaskAfterCompletion()
    {
        // Arrange
        using var db = TestDbFactory.Create();
        var chore = CreateChore(db);
        var schedule = CreateSchedule(db, chore, ScheduleRhythm.Permanent);
        var generator = CreateGenerator(db);

        // Create and "complete" the initial task
        await generator.RunAsync();
        var task = db.ChoreTasks.First();
        task.Status = ChoreTaskStatus.Confirmed;
        task.Schedule = schedule;
        db.SaveChanges();

        // Act
        await generator.RespawnPermanentTaskAsync(task);

        // Assert
        var tasks = db.ChoreTasks.ToList();
        Assert.Equal(2, tasks.Count);
        Assert.Single(tasks.Where(t => t.Status == ChoreTaskStatus.Open));
    }
}
