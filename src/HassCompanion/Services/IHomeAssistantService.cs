namespace HassCompanion.Services;

/// <summary>
/// DTO representing a Home Assistant person entity
/// </summary>
public record HaPerson(string EntityId, string FriendlyName, string? State, string? EntityPicture);

/// <summary>
/// DTO representing a Home Assistant entity state
/// </summary>
public record HaEntityState(string EntityId, string State, Dictionary<string, object?> Attributes);

/// <summary>
/// Abstraction over the Home Assistant REST API.
/// Works both in development (via Long-Lived Token against HA instance)
/// and in production (via Supervisor Token inside the Add-on).
/// </summary>
public interface IHomeAssistantService
{
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
}
