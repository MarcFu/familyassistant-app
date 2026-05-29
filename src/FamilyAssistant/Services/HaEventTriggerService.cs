using System.Collections.Concurrent;
using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

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

    // BUG-004: In-memory debounce to prevent race conditions when concurrent callbacks fire.
    // The DB-based debounce has a TOCTOU race (time-of-check-to-time-of-use) because
    // Task.Run dispatches callbacks concurrently. ConcurrentDictionary + TryUpdate provides
    // atomic compare-and-swap to ensure only one callback wins per debounce window.
    private readonly ConcurrentDictionary<int, DateTime> _lastFiredAt = new();

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
    /// Also seeds the in-memory debounce state from DB for restart resilience.
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

            // Seed in-memory debounce state from DB (survives app restarts)
            foreach (var trigger in activeTriggers.Where(t => t.LastTriggeredAt.HasValue))
            {
                _lastFiredAt.TryAdd(trigger.Id, trigger.LastTriggeredAt!.Value);
            }

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
    /// Uses in-memory CAS debounce to prevent race conditions from concurrent callbacks.
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

                // BUG-004: In-memory debounce with atomic CAS (prevents race condition)
                var now = DateTime.UtcNow;
                var debounceMinutes = Math.Max(trigger.DebounceMins, 1); // At least 1 minute

                while (true)
                {
                    var lastFired = _lastFiredAt.GetOrAdd(trigger.Id, DateTime.MinValue);
                    if ((now - lastFired).TotalMinutes < debounceMinutes)
                    {
                        _logger.LogDebug(
                            "Trigger '{Name}' debounced in-memory ({Elapsed:F1}min < {Debounce}min)",
                            trigger.Name, (now - lastFired).TotalMinutes, debounceMinutes);
                        break;
                    }

                    // Atomic claim: only one concurrent caller wins this
                    if (_lastFiredAt.TryUpdate(trigger.Id, now, lastFired))
                    {
                        // We won — check DB for duplicate as safety net, then create task
                        var duplicateExists = await db.ChoreTasks.AnyAsync(t =>
                            t.ChoreId == trigger.ChoreId
                            && t.DueDate == DateOnly.FromDateTime(DateTime.Today)
                            && t.Status == ChoreTaskStatus.Open
                            && t.OccurrenceLabel == $"Trigger: {trigger.Name}");

                        if (duplicateExists)
                        {
                            _logger.LogDebug(
                                "Trigger '{Name}' skipped: open task already exists for today",
                                trigger.Name);
                        }
                        else
                        {
                            await CreateTriggeredTaskAsync(db, trigger);
                        }
                        break;
                    }

                    // Another thread updated concurrently, retry the check
                }
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

        // Update last triggered timestamp (persists across restarts)
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
