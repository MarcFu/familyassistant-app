using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

/// <summary>
/// Implementation of IChoreTaskQueryService.
/// All filter logic lives here — single source of truth.
/// </summary>
public class ChoreTaskQueryService : IChoreTaskQueryService
{
    private readonly AppDbContext _db;

    public ChoreTaskQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ChoreTask>> GetTodayTasksAsync(int? personId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = BaseQuery()
            .Where(t =>
                (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed || t.Status == ChoreTaskStatus.PendingConfirmation)
                && (t.ScheduleId == null || t.Schedule!.Rhythm != ScheduleRhythm.Permanent)
                && (
                    // Daily/SpecificDays: due today
                    (t.DueDate == today && (t.ScheduleId == null || t.Schedule!.Rhythm == ScheduleRhythm.Daily || t.Schedule!.Rhythm == ScheduleRhythm.SpecificDays))
                    // Weekly/XTimesPerWeek/EveryXWeeks: due within current period (DueDate <= today)
                    || (t.DueDate <= today && t.ScheduleId != null
                        && (t.Schedule!.Rhythm == ScheduleRhythm.Weekly || t.Schedule!.Rhythm == ScheduleRhythm.XTimesPerWeek || t.Schedule!.Rhythm == ScheduleRhythm.EveryXWeeks))
                    // Monthly/EveryXMonths: due within current period (DueDate <= today)
                    || (t.DueDate <= today && t.ScheduleId != null
                        && (t.Schedule!.Rhythm == ScheduleRhythm.Monthly || t.Schedule!.Rhythm == ScheduleRhythm.EveryXMonths))
                ));

        query = ApplyPersonFilter(query, personId);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Chore.Name).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetOpenTasksAsync(int? personId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = BaseQuery()
            .Where(t =>
                (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed)
                && (t.ScheduleId == null || t.Schedule!.Rhythm != ScheduleRhythm.Permanent)
                && (t.DueDate >= today
                    || (t.ScheduleId != null
                        && (t.Schedule!.Rhythm == ScheduleRhythm.Weekly || t.Schedule!.Rhythm == ScheduleRhythm.XTimesPerWeek || t.Schedule!.Rhythm == ScheduleRhythm.EveryXWeeks
                            || t.Schedule!.Rhythm == ScheduleRhythm.Monthly || t.Schedule!.Rhythm == ScheduleRhythm.EveryXMonths))));

        query = ApplyPersonFilter(query, personId);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Chore.Name).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetCompletedThisWeekAsync(int? personId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = GetWeekStart(today);
        var weekStartUtc = weekStart.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var todayEndUtc = today.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var query = BaseQuery()
            .Where(t =>
                (t.Status == ChoreTaskStatus.Confirmed || t.Status == ChoreTaskStatus.PendingConfirmation)
                && (t.ScheduleId == null || t.Schedule!.Rhythm != ScheduleRhythm.Permanent)
                && t.CompletedAt != null
                && t.CompletedAt >= weekStartUtc
                && t.CompletedAt < todayEndUtc);

        query = ApplyPersonFilter(query, personId);
        return await query.OrderByDescending(t => t.CompletedAt).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetOverdueTasksAsync(int? personId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = BaseQuery()
            .Where(t =>
                t.DueDate < today
                && (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed)
                && (t.ScheduleId == null || t.Schedule!.Rhythm != ScheduleRhythm.Permanent));

        query = ApplyPersonFilter(query, personId);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Chore.Name).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetPendingConfirmationAsync(int? personId = null)
    {
        var query = BaseQuery()
            .Where(t => t.Status == ChoreTaskStatus.PendingConfirmation);

        query = ApplyPersonFilter(query, personId);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Chore.Name).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetPermanentTasksAsync(int? personId = null)
    {
        var query = BaseQuery()
            .Where(t => t.Schedule!.Rhythm == ScheduleRhythm.Permanent);

        query = ApplyPersonFilter(query, personId);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Chore.Name).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetThisWeekTasksAsync(int? personId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekEnd = GetWeekStart(today).AddDays(6);

        var query = BaseQuery()
            .Where(t =>
                (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed || t.Status == ChoreTaskStatus.PendingConfirmation)
                && (t.ScheduleId == null || t.Schedule!.Rhythm != ScheduleRhythm.Permanent)
                && (t.DueDate <= weekEnd)
                && (t.DueDate >= today
                    || (t.ScheduleId != null
                        && (t.Schedule!.Rhythm == ScheduleRhythm.Weekly || t.Schedule!.Rhythm == ScheduleRhythm.XTimesPerWeek || t.Schedule!.Rhythm == ScheduleRhythm.EveryXWeeks
                            || t.Schedule!.Rhythm == ScheduleRhythm.Monthly || t.Schedule!.Rhythm == ScheduleRhythm.EveryXMonths))));

        query = ApplyPersonFilter(query, personId);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Chore.Name).Take(100).ToListAsync();
    }

    public async Task<List<ChoreTask>> GetOpenPermanentTasksAsync()
    {
        return await _db.ChoreTasks
            .Include(t => t.Chore)
            .Include(t => t.Schedule)
            .Where(t => t.Schedule!.Rhythm == ScheduleRhythm.Permanent
                && (t.Status == ChoreTaskStatus.Open || t.Status == ChoreTaskStatus.Claimed))
            .OrderBy(t => t.Chore.Name)
            .ToListAsync();
    }

    public async Task<int> GetUnclaimedOpenCountAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return await _db.ChoreTasks
            .CountAsync(t =>
                t.Status == ChoreTaskStatus.Open
                && t.DefaultPersonId == null
                && t.ClaimedByPersonId == null
                && (t.ScheduleId == null || t.Schedule!.Rhythm != ScheduleRhythm.Permanent)
                && t.DueDate >= today && t.DueDate <= today.AddDays(7));
    }

    public async Task<List<ChoreTask>> GetRecentCompletedForPersonAsync(int personId, int maxCount = 5)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = GetWeekStart(today);

        return await _db.ChoreTasks
            .Include(t => t.Chore)
            .Where(t => (t.CompletedByPersonId == personId || t.ClaimedByPersonId == personId)
                && t.Status == ChoreTaskStatus.Confirmed
                && t.CompletedAt != null
                && t.DueDate >= weekStart)
            .OrderByDescending(t => t.CompletedAt)
            .Take(maxCount)
            .ToListAsync();
    }

    #region Private helpers

    private IQueryable<ChoreTask> BaseQuery()
    {
        return _db.ChoreTasks
            .Include(t => t.Chore)
            .Include(t => t.Schedule)
            .Include(t => t.DefaultPerson)
            .Include(t => t.ClaimedByPerson)
            .Include(t => t.CompletedByPerson)
            .Include(t => t.Comments);
    }

    private static IQueryable<ChoreTask> ApplyPersonFilter(IQueryable<ChoreTask> query, int? personId)
    {
        if (!personId.HasValue) return query;

        return query.Where(t =>
            t.DefaultPersonId == personId
            || t.ClaimedByPersonId == personId
            || t.CompletedByPersonId == personId
            || (t.DefaultPersonId == null && t.ClaimedByPersonId == null));
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        return date.AddDays(-(int)date.DayOfWeek + (int)DayOfWeek.Monday);
    }

    #endregion
}
