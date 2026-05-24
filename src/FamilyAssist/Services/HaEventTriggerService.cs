using FamilyAssist.Data;
using FamilyAssist.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssist.Services;

/// <summary>
/// Background service that monitors HA entity state changes via WebSocket
/// and creates ad-hoc tasks when configured triggers fire.
/// Handles debouncing to prevent state-flapping from creating duplicates.
/// </summary>
public class HaEventTriggerService : BackgroundService
{
    private readonly IHomeAssistantService _haService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HaEventTriggerService> _logger;

    // Active subscriptions: entityId → subscriptionId
    private readonly Dictionary<string, int> _activeSubscriptions = new();
    private readonly object _lock = new();

    public HaEventTriggerService(
        IHomeAssistantService haService,
        IServiceScopeFactory scopeFactory,
        ILogger<HaEventTriggerService> logger)
    {
        _haService = haService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for WebSocket to be established (HaWebSocketStartupService connects first)
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_haService.IsWebSocketConnected)
            {
                await SyncSubscriptionsAsync(stoppingToken);
                break;
            }

            _logger.LogDebug("Waiting for WebSocket connection before subscribing triggers...");
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        // Periodically re-sync subscriptions (in case triggers are added/removed/toggled)
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            if (_haService.IsWebSocketConnected)
            {
                await SyncSubscriptionsAsync(stoppingToken);
            }
        }
    }

    /// <summary>
    /// Loads active triggers from DB and ensures we have WebSocket subscriptions for each entity.
    /// Removes subscriptions for entities that no longer have active triggers.
    /// </summary>
    private async Task SyncSubscriptionsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeTriggers = await db.EventTriggers
                .Where(t => t.IsActive)
                .ToListAsync(ct);

            var desiredEntities = activeTriggers
                .Select(t => t.EntityId)
                .Distinct()
                .ToHashSet();

            // Remove subscriptions for entities no longer needed
            List<KeyValuePair<string, int>> toRemove;
            lock (_lock)
            {
                toRemove = _activeSubscriptions
                    .Where(kvp => !desiredEntities.Contains(kvp.Key))
                    .ToList();
            }

            foreach (var (entityId, subId) in toRemove)
            {
                try
                {
                    await _haService.UnsubscribeAsync(subId, ct);
                    _logger.LogInformation("Unsubscribed from {Entity} (sub={SubId})", entityId, subId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unsubscribe from {Entity}", entityId);
                }

                lock (_lock) { _activeSubscriptions.Remove(entityId); }
            }

            // Subscribe to new entities
            HashSet<string> alreadySubscribed;
            lock (_lock) { alreadySubscribed = _activeSubscriptions.Keys.ToHashSet(); }

            foreach (var entityId in desiredEntities.Where(e => !alreadySubscribed.Contains(e)))
            {
                try
                {
                    var subId = await _haService.SubscribeStateChangedAsync(entityId, OnStateChanged, ct);
                    lock (_lock) { _activeSubscriptions[entityId] = subId; }
                    _logger.LogInformation("Subscribed to state changes for {Entity} (sub={SubId})", entityId, subId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to subscribe to {Entity}", entityId);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error syncing event trigger subscriptions");
        }
    }

    /// <summary>
    /// Called when a subscribed entity changes state.
    /// Checks all active triggers for this entity and fires if conditions met.
    /// </summary>
    private async Task OnStateChanged(string entityId, string oldState, string newState)
    {
        _logger.LogInformation("State change received: {Entity} {Old} → {New}", entityId, oldState, newState);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var triggers = await db.EventTriggers
                .Include(t => t.Chore)
                .Where(t => t.IsActive && t.EntityId == entityId)
                .ToListAsync();

            foreach (var trigger in triggers)
            {
                // Check if the new state matches the trigger condition
                if (!string.Equals(trigger.TriggerState, newState, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Debounce: skip if triggered too recently
                if (trigger.LastTriggeredAt.HasValue)
                {
                    var elapsed = DateTime.UtcNow - trigger.LastTriggeredAt.Value;
                    if (elapsed.TotalMinutes < trigger.DebounceMins)
                    {
                        _logger.LogDebug(
                            "Trigger '{Name}' debounced ({Elapsed:F0}min < {Debounce}min)",
                            trigger.Name, elapsed.TotalMinutes, trigger.DebounceMins);
                        continue;
                    }
                }

                // Fire: create ad-hoc task
                await CreateTriggeredTaskAsync(db, trigger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing state change for {Entity}", entityId);
        }
    }

    private async Task CreateTriggeredTaskAsync(AppDbContext db, EventTrigger trigger)
    {
        var task = new ChoreTask
        {
            ChoreId = trigger.ChoreId,
            ScheduleId = null, // Ad-hoc
            DueDate = DateOnly.FromDateTime(DateTime.Today),
            OccurrenceIndex = 1,
            OccurrenceLabel = $"Trigger: {trigger.Name}",
            DefaultPersonId = null,
            Status = ChoreTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        db.ChoreTasks.Add(task);

        // Update last triggered timestamp
        trigger.LastTriggeredAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Trigger '{Name}' fired: created task for chore '{Chore}' (entity={Entity}, state={State})",
            trigger.Name, trigger.Chore.Name, trigger.EntityId, trigger.TriggerState);
    }

    /// <summary>
    /// Force re-sync subscriptions (called when triggers are changed via UI).
    /// </summary>
    public void NotifyTriggersChanged()
    {
        // The periodic sync will pick it up within 2 minutes.
        // For immediate effect, we could signal here — but 2 min delay is acceptable.
        _logger.LogInformation("Trigger configuration changed, will re-sync subscriptions on next cycle");
    }
}
