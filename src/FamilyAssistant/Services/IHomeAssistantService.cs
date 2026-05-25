namespace FamilyAssistant.Services;

/// <summary>
/// DTO representing a Home Assistant person entity
/// </summary>
public record HaPerson(string EntityId, string FriendlyName, string? State, string? EntityPicture, string? UserId);

/// <summary>
/// DTO representing a Home Assistant entity state
/// </summary>
public record HaEntityState(string EntityId, string State, Dictionary<string, object?> Attributes);

/// <summary>
/// Registry metadata for an entity (area, device, platform info).
/// Used by the EntityPicker for richer display.
/// </summary>
public record HaEntityRegistryInfo(
    string EntityId,
    string? AreaName,
    string? DeviceName,
    string? Platform
);

/// <summary>
/// A single captured WebSocket event for the live event log.
/// </summary>
public record HaEventLogEntry(DateTime Timestamp, string EventType, string EntityId, string? OldState, string? NewState, string? RawData);

/// <summary>
/// DTO containing the user's frontend preferences from HA (theme, language, colors).
/// Retrieved via WebSocket command: frontend/get_user_data (key: "core").
/// DarkMode: true=forced dark, false=forced light, null=auto (follow OS preference).
/// </summary>
public record HaUserFrontendData(
    bool? DarkMode,
    string? Language,
    string? PrimaryColor,
    string? AccentColor
);

/// <summary>
/// Callback for state change subscriptions.
/// </summary>
public delegate Task StateChangedCallback(string entityId, string oldState, string newState);

/// <summary>
/// Abstraction over the Home Assistant REST + WebSocket API.
/// Works both in development (via Long-Lived Token against HA instance)
/// and in production (via Supervisor Token inside the Add-on).
/// All HA communication goes through this proxy.
/// </summary>
public interface IHomeAssistantService
{
    // ─── REST API ───────────────────────────────────────────────

    /// <summary>
    /// Get all person entities from Home Assistant
    /// </summary>
    Task<IReadOnlyList<HaPerson>> GetPersonsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current state of an entity
    /// </summary>
    Task<HaEntityState?> GetEntityStateAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    /// Set/update the state of an entity (used for syncing credit balance etc.)
    /// </summary>
    Task SetEntityStateAsync(string entityId, string state, Dictionary<string, object?>? attributes = null, CancellationToken ct = default);

    /// <summary>
    /// Call a Home Assistant service (e.g., switch.turn_on to block a device)
    /// </summary>
    Task CallServiceAsync(string domain, string service, object? data = null, CancellationToken ct = default);

    /// <summary>
    /// Test connectivity to Home Assistant
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Proxy an image from Home Assistant (e.g., entity pictures)
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetImageAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Get all entities matching a domain (e.g., "switch", "todo")
    /// </summary>
    Task<IReadOnlyList<HaEntityState>> GetEntitiesByDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Get registry metadata (area/device names) for entities.
    /// Uses WebSocket registry commands, cached for performance.
    /// Returns a lookup: entity_id → HaEntityRegistryInfo
    /// </summary>
    Task<IReadOnlyDictionary<string, HaEntityRegistryInfo>> GetEntityRegistryAsync(CancellationToken ct = default);

    // ─── WebSocket API ──────────────────────────────────────────

    /// <summary>
    /// Establish and authenticate the WebSocket connection.
    /// Called once at app start by a BackgroundService.
    /// </summary>
    Task ConnectWebSocketAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether the WebSocket is currently connected.
    /// </summary>
    bool IsWebSocketConnected { get; }

    /// <summary>
    /// Get the HA frontend dark mode preference for the token owner.
    /// Uses WebSocket command: frontend/get_user_data (key: "core").
    /// </summary>
    Task<bool> GetUserDarkModeAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the HA frontend language preference for the token owner.
    /// Uses WebSocket command: frontend/get_user_data (key: "core").
    /// Returns ISO language code (e.g., "de", "en") or null if unavailable.
    /// </summary>
    Task<string?> GetUserLanguageAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all user frontend preferences (dark mode, language, colors) in a single WebSocket call.
    /// Uses WebSocket command: frontend/get_user_data (key: "core").
    /// </summary>
    Task<HaUserFrontendData> GetUserFrontendDataAsync(CancellationToken ct = default);

    /// <summary>
    /// Subscribe to state changes for a specific entity.
    /// Returns a subscription ID for later unsubscribing.
    /// </summary>
    Task<int> SubscribeStateChangedAsync(string entityId, StateChangedCallback callback, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe from a previous event subscription.
    /// </summary>
    Task UnsubscribeAsync(int subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Simulate a state_changed event for testing. Invokes all matching subscription callbacks.
    /// </summary>
    Task SimulateStateChangeAsync(string entityId, string oldState, string newState);

    // ─── Event Log ──────────────────────────────────────────────

    /// <summary>
    /// Whether event logging is active (captures all events to buffer).
    /// </summary>
    bool IsEventLoggingEnabled { get; set; }

    /// <summary>
    /// Start monitoring all HA events (subscribes to all event types via WebSocket).
    /// </summary>
    Task StartEventMonitorAsync(CancellationToken ct = default);

    /// <summary>
    /// Stop monitoring all HA events (unsubscribes).
    /// </summary>
    Task StopEventMonitorAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the most recent captured events (newest first). Max ~200 entries.
    /// </summary>
    IReadOnlyList<HaEventLogEntry> GetRecentEvents();

    /// <summary>
    /// Clear the event log buffer.
    /// </summary>
    void ClearEventLog();
}
