namespace FamilyAssist.Models;

/// <summary>
/// A managed device belonging to a person, with associated HA entities
/// for internet control, gaming detection, and gaming blocking.
/// </summary>
public class PersonDevice
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>
    /// Human-readable label (e.g., "Alex PC", "Alex Handy")
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Device role determines how enforcement rules apply
    /// </summary>
    public DeviceRole Role { get; set; } = DeviceRole.Shared;

    /// <summary>
    /// HA switch entity that controls full internet access for this device
    /// (e.g., "switch.unifi_alex_pc")
    /// </summary>
    public required string InternetSwitchEntity { get; set; }

    /// <summary>
    /// If true, turning the switch ON means internet is BLOCKED (inverted logic).
    /// Default: false (ON = internet allowed).
    /// </summary>
    public bool InvertInternetSwitch { get; set; }

    /// <summary>
    /// Optional: HA binary_sensor that detects gaming activity on this device
    /// (e.g., "binary_sensor.alex_pc_gaming" from UniFi DPI)
    /// </summary>
    public string? DetectionEntity { get; set; }

    /// <summary>
    /// Optional: HA switch entity that blocks only gaming traffic
    /// (e.g., "switch.unifi_alex_pc_gaming_block" via UniFi Traffic Rule)
    /// </summary>
    public string? GamingBlockEntity { get; set; }

    /// <summary>
    /// If true, turning the gaming block switch ON means gaming is ALLOWED (inverted logic).
    /// Default: false (ON = gaming blocked).
    /// </summary>
    public bool InvertGamingBlock { get; set; }

    /// <summary>
    /// Navigation: Rules that affect this device
    /// </summary>
    public ICollection<InternetRule> InternetRules { get; set; } = [];
}

/// <summary>
/// The role of a device determines which enforcement actions apply.
/// Values must be explicit for DB storage.
/// </summary>
public enum DeviceRole
{
    /// <summary>
    /// Device is used for both school/work and gaming.
    /// Nachtruhe: internet OFF. Budget depleted: only gaming blocked.
    /// </summary>
    Shared = 0,

    /// <summary>
    /// Device is used exclusively for school/work.
    /// Nachtruhe: internet OFF. Gaming rules have no effect.
    /// </summary>
    SchoolOnly = 1,

    /// <summary>
    /// Device is used exclusively for gaming (console, etc.)
    /// Nachtruhe: internet OFF. Budget depleted: internet OFF (entire device).
    /// </summary>
    GamingOnly = 2
}
