namespace FamilyAssistant.Services;

/// <summary>
/// Reads the HA frontend user preferences (theme, colors, language) and exposes them to the UI.
/// Builds on IHomeAssistantService (WebSocket proxy).
/// </summary>
public class HaThemeService
{
    private readonly IHomeAssistantService _haService;
    private readonly ILogger<HaThemeService> _logger;

    public HaThemeService(IHomeAssistantService haService, ILogger<HaThemeService> logger)
    {
        _haService = haService;
        _logger = logger;
    }

    /// <summary>
    /// Dark mode preference from HA: true=dark, false=light, null=auto (follow OS preference).
    /// Defaults to null (auto) until WebSocket delivers real value.
    /// </summary>
    public bool? DarkModePreference { get; private set; }

    /// <summary>
    /// Dev override for dark mode. Has highest priority over HA preference.
    /// null=no override (use HA/OS), true=forced dark, false=forced light.
    /// </summary>
    public bool? DevDarkModeOverride { get; set; }

    /// <summary>
    /// Whether to use light-styled achievement badges in light mode (Variante B).
    /// Only applies when resolved mode is light.
    /// </summary>
    public bool DevLightBadges { get; set; }

    /// <summary>
    /// Dev override for primary color. Null = use HA value (or app default).
    /// </summary>
    public string? DevPrimaryColorOverride { get; set; }

    /// <summary>
    /// Dev override for accent/secondary color. Null = use HA value (or app default).
    /// </summary>
    public string? DevAccentColorOverride { get; set; }

    /// <summary>
    /// Gets the effective dark mode preference considering dev override > HA preference > null (auto).
    /// </summary>
    public bool? EffectiveDarkModePreference => DevDarkModeOverride ?? DarkModePreference;

    /// <summary>
    /// Resolved dark mode state for convenience (applies system default=true when auto).
    /// Use EffectiveDarkModePreference for the raw tri-state value.
    /// </summary>
    public bool IsDarkMode => EffectiveDarkModePreference ?? true;

    /// <summary>
    /// Effective primary color (dev override > HA > null=app default).
    /// </summary>
    public string? EffectivePrimaryColor => DevPrimaryColorOverride ?? PrimaryColor;

    /// <summary>
    /// Effective accent color (dev override > HA > null=app default).
    /// </summary>
    public string? EffectiveAccentColor => DevAccentColorOverride ?? AccentColor;

    /// <summary>
    /// Primary color from HA user settings (hex string, e.g. "#03a9f4"). Null = use app default.
    /// </summary>
    public string? PrimaryColor { get; private set; }

    /// <summary>
    /// Accent color from HA user settings (hex string, e.g. "#ff9800"). Null = use app default.
    /// </summary>
    public string? AccentColor { get; private set; }

    /// <summary>
    /// Language from HA user settings (ISO code, e.g. "de", "en"). Null = use app default.
    /// </summary>
    public string? Language { get; private set; }

    /// <summary>
    /// Timestamp of the last successful refresh from HA.
    /// </summary>
    public DateTime? LastRefreshed { get; private set; }

    /// <summary>
    /// Status text of last refresh attempt (for diagnostics).
    /// </summary>
    public string? LastRefreshStatus { get; private set; }

    /// <summary>
    /// Raw JSON response from last GetUserFrontendDataAsync call (for diagnostics).
    /// </summary>
    public string? LastRawResponse { get; private set; }

    /// <summary>
    /// Fired when any preference changes (theme, colors, language, or dev override).
    /// </summary>
    public event Action? ThemeChanged;

    /// <summary>
    /// Notify subscribers that theme settings have changed (e.g. after dev override change).
    /// </summary>
    public void NotifyChanged() => ThemeChanged?.Invoke();

    /// <summary>
    /// Reads all frontend preferences from HA via WebSocket. Called after WebSocket connects and periodically.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_haService.IsWebSocketConnected)
            {
                LastRefreshStatus = "WebSocket not connected";
                LastRefreshed = DateTime.Now;
                _logger.LogWarning("ThemeService.RefreshAsync skipped: WebSocket not connected");
                return;
            }

            var data = await _haService.GetUserFrontendDataAsync(ct);
            LastRawResponse = data.RawJson;
            var changed = false;

            if (data.DarkMode != DarkModePreference)
            {
                DarkModePreference = data.DarkMode;
                changed = true;
            }

            if (data.PrimaryColor != PrimaryColor)
            {
                PrimaryColor = data.PrimaryColor;
                changed = true;
            }

            if (data.AccentColor != AccentColor)
            {
                AccentColor = data.AccentColor;
                changed = true;
            }

            if (data.Language != Language)
            {
                Language = data.Language;
                changed = true;
            }

            LastRefreshed = DateTime.Now;

            if (changed)
            {
                LastRefreshStatus = $"OK (lang={Language ?? "null"}, dark={DarkModePreference?.ToString() ?? "auto"})";
                _logger.LogInformation("Theme updated from HA: darkMode={DarkMode}, primary={Primary}, accent={Accent}, lang={Lang}",
                    DarkModePreference?.ToString() ?? "auto", PrimaryColor, AccentColor, Language);
                ThemeChanged?.Invoke();
            }
            else
            {
                var hasData = Language != null || PrimaryColor != null || AccentColor != null || DarkModePreference != null;
                LastRefreshStatus = hasData
                    ? $"OK, no change (lang={Language})"
                    : "OK but all values null — HA returned empty data";
            }
        }
        catch (Exception ex)
        {
            LastRefreshStatus = $"Error: {ex.Message}";
            LastRefreshed = DateTime.Now;
            _logger.LogWarning(ex, "Failed to read frontend data from HA, keeping current state");
        }
    }
}
