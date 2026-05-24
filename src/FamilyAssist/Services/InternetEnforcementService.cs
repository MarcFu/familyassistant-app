using FamilyAssist.Data;
using FamilyAssist.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssist.Services;

/// <summary>
/// Background service that enforces internet rules by switching HA entities.
/// Phase 1: TimeWindow rules only.
/// Runs every 60 seconds and evaluates all active rules against the current time.
/// </summary>
public class InternetEnforcementService : BackgroundService
{
    private readonly IHomeAssistantService _haService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InternetEnforcementService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    public InternetEnforcementService(
        IHomeAssistantService haService,
        IServiceScopeFactory scopeFactory,
        ILogger<InternetEnforcementService> logger)
    {
        _haService = haService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for HA connection to be established
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        _logger.LogInformation("InternetEnforcementService started. Checking rules every {Interval}s.", CheckInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_haService.IsWebSocketConnected)
                {
                    await EvaluateRulesAsync(stoppingToken);
                }
                else
                {
                    _logger.LogDebug("HA not connected, skipping enforcement cycle.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during internet rule enforcement cycle.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task EvaluateRulesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rules = await db.InternetRules
            .Include(r => r.AffectedDevices)
            .Include(r => r.Person)
            .Where(r => r.IsActive && r.RuleMode == InternetRuleMode.TimeWindow)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var today = GetTodayFlag();

        // Group by device: a device may be referenced by multiple rules.
        // A device should be ON (internet allowed) if ANY rule allows it right now.
        var deviceDecisions = new Dictionary<int, DeviceDecision>();

        foreach (var rule in rules)
        {
            var isWithinWindow = IsWithinTimeWindow(rule, now, today);

            foreach (var device in rule.AffectedDevices)
            {
                if (!deviceDecisions.TryGetValue(device.Id, out var decision))
                {
                    decision = new DeviceDecision(device);
                    deviceDecisions[device.Id] = decision;
                }

                // For Internet target: rule defines when internet is ALLOWED
                // For Gaming target: rule defines when gaming is ALLOWED
                if (isWithinWindow && rule.Target == InternetRuleTarget.Internet)
                    decision.InternetAllowed = true;
                if (isWithinWindow && rule.Target == InternetRuleTarget.Gaming)
                    decision.GamingAllowed = true;

                // Track if any rule of each target type references this device
                if (rule.Target == InternetRuleTarget.Internet)
                    decision.HasInternetRule = true;
                if (rule.Target == InternetRuleTarget.Gaming)
                    decision.HasGamingRule = true;
            }
        }

        // Apply decisions
        foreach (var (_, decision) in deviceDecisions)
        {
            await ApplyDecisionAsync(decision, ct);
        }
    }

    private async Task ApplyDecisionAsync(DeviceDecision decision, CancellationToken ct)
    {
        var device = decision.Device;

        // Internet switch: only enforce if there's a rule targeting internet for this device
        if (decision.HasInternetRule)
        {
            var shouldBeOn = decision.InternetAllowed;
            // If inverted: ON = blocked, so we flip the logic
            if (device.InvertInternetSwitch)
                shouldBeOn = !shouldBeOn;

            await SetSwitchStateAsync(device.InternetSwitchEntity, shouldBeOn, ct);
        }

        // Gaming block switch: only enforce if there's a rule targeting gaming AND device has a gaming block entity
        if (decision.HasGamingRule && device.GamingBlockEntity is not null)
        {
            var gamingBlocked = !decision.GamingAllowed;
            // Gaming block entity: default ON = blocked
            // If inverted: ON = gaming allowed
            if (device.InvertGamingBlock)
                gamingBlocked = !gamingBlocked;

            await SetSwitchStateAsync(device.GamingBlockEntity, gamingBlocked, ct);
        }

        // For GamingOnly devices without a separate gaming block entity:
        // If gaming is not allowed, turn off internet entirely
        if (decision.HasGamingRule && device.GamingBlockEntity is null && device.Role == DeviceRole.GamingOnly)
        {
            if (!decision.GamingAllowed)
            {
                var shouldBeOn = false;
                if (device.InvertInternetSwitch)
                    shouldBeOn = !shouldBeOn;

                await SetSwitchStateAsync(device.InternetSwitchEntity, shouldBeOn, ct);
            }
        }
    }

    private async Task SetSwitchStateAsync(string entityId, bool turnOn, CancellationToken ct)
    {
        try
        {
            var service = turnOn ? "turn_on" : "turn_off";
            await _haService.CallServiceAsync("switch", service,
                new { entity_id = entityId }, ct);

            _logger.LogDebug("Switch {Entity} → {State}", entityId, turnOn ? "ON" : "OFF");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set switch {Entity} to {State}", entityId, turnOn ? "ON" : "OFF");
        }
    }

    private static bool IsWithinTimeWindow(InternetRule rule, TimeOnly now, DaysOfWeek today)
    {
        // Check day of week
        if ((rule.ApplicableDays & today) == 0)
            return false;

        if (rule.WindowStart is null || rule.WindowEnd is null)
            return false;

        var start = rule.WindowStart.Value;
        var end = rule.WindowEnd.Value;

        // Handle overnight windows (e.g., 22:00 - 06:00)
        if (start <= end)
            return now >= start && now <= end;
        else
            return now >= start || now <= end;
    }

    private static DaysOfWeek GetTodayFlag()
    {
        return DateTime.Now.DayOfWeek switch
        {
            System.DayOfWeek.Monday => DaysOfWeek.Monday,
            System.DayOfWeek.Tuesday => DaysOfWeek.Tuesday,
            System.DayOfWeek.Wednesday => DaysOfWeek.Wednesday,
            System.DayOfWeek.Thursday => DaysOfWeek.Thursday,
            System.DayOfWeek.Friday => DaysOfWeek.Friday,
            System.DayOfWeek.Saturday => DaysOfWeek.Saturday,
            System.DayOfWeek.Sunday => DaysOfWeek.Sunday,
            _ => DaysOfWeek.None
        };
    }

    private class DeviceDecision
    {
        public PersonDevice Device { get; }
        public bool InternetAllowed { get; set; }
        public bool GamingAllowed { get; set; }
        public bool HasInternetRule { get; set; }
        public bool HasGamingRule { get; set; }

        public DeviceDecision(PersonDevice device)
        {
            Device = device;
        }
    }
}
