using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

/// <summary>
/// Syncs data to Home Assistant:
/// - Credit balance as sensor entities
/// - Open tasks into HA todo lists
/// </summary>
public class HomeAssistantSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HomeAssistantSyncService> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public HomeAssistantSyncService(IServiceScopeFactory scopeFactory, ILogger<HomeAssistantSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncCreditsToHaAsync(stoppingToken);
                await SyncTasksToTodoListsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HA sync cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// Syncs credit balance for each person as a HA sensor entity.
    /// Creates entities like sensor.familyassistant_alex_credits
    /// </summary>
    private async Task SyncCreditsToHaAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var haService = scope.ServiceProvider.GetRequiredService<IHomeAssistantService>();

        var persons = await db.Persons.ToListAsync(ct);

        foreach (var person in persons)
        {
            var entityId = $"sensor.familyassistant_{SanitizeName(person.Name)}_credits";
            var attributes = new Dictionary<string, object?>
            {
                ["friendly_name"] = $"{person.Name} Credits",
                ["unit_of_measurement"] = "Credits",
                ["icon"] = "mdi:star-circle",
                ["role"] = person.Role.ToString(),
                ["is_paused"] = person.IsPaused
            };

            try
            {
                await haService.SetEntityStateAsync(entityId, person.Credits.ToString(), attributes, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync credits for {Person}", person.Name);
            }
        }

        _logger.LogDebug("Synced credits for {Count} persons to HA", persons.Count);
    }

    /// <summary>
    /// Syncs open/pending tasks into HA todo lists for each person.
    /// </summary>
    private async Task SyncTasksToTodoListsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var haService = scope.ServiceProvider.GetRequiredService<IHomeAssistantService>();

        var persons = await db.Persons
            .Where(p => p.HaTodoEntityId != null)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekEnd = today.AddDays(7);

        foreach (var person in persons)
        {
            if (string.IsNullOrWhiteSpace(person.HaTodoEntityId)) continue;

            try
            {
                // Get tasks assigned to or claimed by this person for the next 7 days
                var tasks = await db.ChoreTasks
                    .Include(t => t.Chore)
                    .Where(t => (t.DefaultPersonId == person.Id || t.ClaimedByPersonId == person.Id)
                        && t.DueDate >= today && t.DueDate <= weekEnd
                        && (t.Status == ChoreTaskStatus.Open
                            || t.Status == ChoreTaskStatus.Claimed
                            || t.Status == ChoreTaskStatus.PendingConfirmation))
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.Chore.Name)
                    .ToListAsync(ct);

                // Sync to HA todo list
                // First, we set the todo list state with the count
                var attributes = new Dictionary<string, object?>
                {
                    ["friendly_name"] = $"{person.Name} Aufgaben",
                    ["icon"] = "mdi:clipboard-check-outline"
                };

                await haService.SetEntityStateAsync(
                    person.HaTodoEntityId,
                    tasks.Count.ToString(),
                    attributes,
                    ct);

                // For actual todo items, use the todo.add_item / todo.update_item services
                // This requires the person to have a real HA todo list entity (from Local To-do integration)
                foreach (var task in tasks)
                {
                    var summary = $"{task.Chore.Name}" +
                        (task.OccurrenceLabel is not null ? $" ({task.OccurrenceLabel})" : "") +
                        $" — {task.Chore.CreditsReward} Credits";

                    var status = task.Status switch
                    {
                        ChoreTaskStatus.PendingConfirmation => "needs_action", // completed but unconfirmed
                        _ => "needs_action"
                    };

                    // Use the todo service to add/update items
                    try
                    {
                        await haService.CallServiceAsync("todo", "add_item", new
                        {
                            entity_id = person.HaTodoEntityId,
                            item = summary,
                            due_date = task.DueDate.ToString("yyyy-MM-dd")
                        }, ct);
                    }
                    catch
                    {
                        // Item might already exist — that's okay for now
                        // A more sophisticated approach would track HA todo item UIDs
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync todo list for {Person}", person.Name);
            }
        }
    }

    private static string SanitizeName(string name)
    {
        return name.ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")
            .Replace(" ", "_")
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .Aggregate("", (s, c) => s + c);
    }
}
