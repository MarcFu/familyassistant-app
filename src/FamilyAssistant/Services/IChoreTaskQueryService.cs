using FamilyAssistant.Models;

namespace FamilyAssistant.Services;

/// <summary>
/// Query service for ChoreTask filtering.
/// Encapsulates the business logic of what "Today", "Open", "Completed" etc. mean.
/// Read-only — no mutations.
/// </summary>
public interface IChoreTaskQueryService
{
    /// <summary>
    /// Actionable tasks for today: Open/Claimed/PendingConfirmation, scoped to the current period per rhythm.
    /// </summary>
    Task<List<ChoreTask>> GetTodayTasksAsync(int? personId = null);

    /// <summary>
    /// Open or Claimed tasks (not permanent), including period-based tasks still within their window.
    /// </summary>
    Task<List<ChoreTask>> GetOpenTasksAsync(int? personId = null);

    /// <summary>
    /// Confirmed or PendingConfirmation tasks completed this week.
    /// </summary>
    Task<List<ChoreTask>> GetCompletedThisWeekAsync(int? personId = null);

    /// <summary>
    /// Tasks that are past their due date and still Open/Claimed.
    /// </summary>
    Task<List<ChoreTask>> GetOverdueTasksAsync(int? personId = null);

    /// <summary>
    /// Tasks awaiting parent confirmation (PendingConfirmation status).
    /// </summary>
    Task<List<ChoreTask>> GetPendingConfirmationAsync(int? personId = null);

    /// <summary>
    /// All permanent-rhythm tasks (any status).
    /// </summary>
    Task<List<ChoreTask>> GetPermanentTasksAsync(int? personId = null);

    /// <summary>
    /// Actionable tasks for this week: Open/Claimed/PendingConfirmation with DueDate within current week.
    /// </summary>
    Task<List<ChoreTask>> GetThisWeekTasksAsync(int? personId = null);

    /// <summary>
    /// Open permanent tasks (for the expander on the tasks page).
    /// </summary>
    Task<List<ChoreTask>> GetOpenPermanentTasksAsync();

    /// <summary>
    /// Count of unclaimed open tasks within the next 7 days (for bonus hint).
    /// </summary>
    Task<int> GetUnclaimedOpenCountAsync();

    /// <summary>
    /// Recent completed tasks for a specific person this week (for dashboard).
    /// </summary>
    Task<List<ChoreTask>> GetRecentCompletedForPersonAsync(int personId, int maxCount = 5);
}
