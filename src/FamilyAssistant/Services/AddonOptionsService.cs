using System.Text.Json;
using System.Text.Json.Serialization;

namespace FamilyAssistant.Services;

/// <summary>
/// Reads HA add-on options from /data/options.json (mounted by Supervisor).
/// In local dev mode (no SUPERVISOR_TOKEN), DevMode defaults to true.
/// In add-on mode, DevMode defaults to false (must be enabled in config).
/// </summary>
public sealed class AddonOptionsService
{
    private readonly AddonOptions _options;

    public AddonOptionsService(ILogger<AddonOptionsService> logger)
    {
        const string optionsPath = "/data/options.json";
        var isAddonMode = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN"));

        if (File.Exists(optionsPath))
        {
            try
            {
                var json = File.ReadAllText(optionsPath);
                RawJson = json;
                _options = JsonSerializer.Deserialize<AddonOptions>(json, JsonOptions) ?? new AddonOptions();
                logger.LogInformation("Loaded add-on options: DevMode={DevMode}", _options.DevMode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read {Path}, using defaults", optionsPath);
                _options = new AddonOptions();
            }
        }
        else if (!isAddonMode)
        {
            // Local development: enable dev features by default
            _options = new AddonOptions { DevMode = true };
            logger.LogDebug("Local dev mode detected, DevMode enabled by default");
        }
        else
        {
            _options = new AddonOptions();
            logger.LogWarning("Add-on mode but no options file at {Path}, using defaults", optionsPath);
        }
    }

    public bool DevMode => _options.DevMode;

    /// <summary>
    /// Raw JSON content of /data/options.json for diagnostics.
    /// </summary>
    public string? RawJson { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class AddonOptions
    {
        [JsonPropertyName("dev_mode")]
        public bool DevMode { get; set; }
    }
}
