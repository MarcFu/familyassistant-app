namespace FamilyAssistant.Services;

/// <summary>
/// Provides theme/language preferences to the UI. Reads per-user data from HA config storage files
/// via HaUserPreferencesService. Maintains dev overrides for testing.
/// </summary>
public class HaThemeService
{
    private readonly HaUserPreferencesService _prefsService;
    private readonly ILogger<HaThemeService> _logger;

    public HaThemeService(HaUserPreferencesService prefsService, ILogger<HaThemeService> logger)
    {
        _prefsService = prefsService;
        _logger = logger;
    }

    // ─── Current user context ────────────────────────────────────────

    /// <summary>
    /// The HA user ID currently loaded (set by MainLayout on init from X-Remote-User-Id header).
    /// </summary>
    public string? CurrentUserId { get; private set; }

    // ─── HA-sourced preferences ──────────────────────────────────────

    /// <summary>
    /// Dark mode preference from HA: true=dark, false=light, null=auto (follow OS preference).
    /// </summary>
    public bool? DarkModePreference { get; private set; }

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

    // ─── Dev overrides ───────────────────────────────────────────────

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

    // ─── Effective values (dev override > HA > defaults) ─────────────

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

    // ─── Diagnostics ─────────────────────────────────────────────────

    /// <summary>
    /// Timestamp of the last successful refresh.
    /// </summary>
    public DateTime? LastRefreshed { get; private set; }

    /// <summary>
    /// Status text of last refresh attempt (for diagnostics).
    /// </summary>
    public string? LastRefreshStatus { get; private set; }

    /// <summary>
    /// File path being read (for diagnostics).
    /// </summary>
    public string? LastFilePath { get; private set; }

    // ─── Events ──────────────────────────────────────────────────────

    /// <summary>
    /// Fired when any preference changes (theme, colors, language, or dev override).
    /// </summary>
    public event Action? ThemeChanged;

    /// <summary>
    /// Notify subscribers that theme settings have changed (e.g. after dev override change).
    /// </summary>
    public void NotifyChanged() => ThemeChanged?.Invoke();

    // ─── Core methods ────────────────────────────────────────────────

    /// <summary>
    /// Reads preferences for a specific HA user from the config storage file.
    /// Called by MainLayout on circuit init with the user ID from X-Remote-User-Id header.
    /// </summary>
    public void RefreshForUser(string? userId)
    {
        CurrentUserId = userId;
        LastFilePath = _prefsService.GetFilePath(userId);

        if (string.IsNullOrEmpty(userId))
        {
            LastRefreshStatus = "No user ID available";
            LastRefreshed = DateTime.Now;
            _logger.LogDebug("ThemeService: No user ID, keeping defaults");
            return;
        }

        var prefs = _prefsService.Refresh(userId);
        ApplyPreferences(prefs, userId);
    }

    /// <summary>
    /// Gets preferences from cache (non-blocking). Used by AppSettingsCultureProvider on each request.
    /// </summary>
    public UserPreferences? GetCachedPreferences(string? userId)
    {
        return _prefsService.GetPreferences(userId);
    }

    private void ApplyPreferences(UserPreferences? prefs, string userId)
    {
        LastRefreshed = DateTime.Now;

        if (prefs is null)
        {
            LastRefreshStatus = _prefsService.GetLastStatus(userId) ?? "No preferences found";
            _logger.LogDebug("No preferences available for user {UserId}", userId);
            return;
        }

        var changed = false;

        if (prefs.DarkMode != DarkModePreference)
        {
            DarkModePreference = prefs.DarkMode;
            changed = true;
        }

        if (prefs.PrimaryColor != PrimaryColor)
        {
            PrimaryColor = prefs.PrimaryColor;
            changed = true;
        }

        if (prefs.AccentColor != AccentColor)
        {
            AccentColor = prefs.AccentColor;
            changed = true;
        }

        if (prefs.Language != Language)
        {
            Language = prefs.Language;
            changed = true;
        }

        LastRefreshStatus = $"OK (lang={Language ?? "null"}, dark={DarkModePreference?.ToString() ?? "auto"})";

        if (changed)
        {
            _logger.LogInformation(
                "Theme updated from file for user {UserId}: lang={Lang}, dark={Dark}, primary={Primary}, accent={Accent}",
                userId, Language, DarkModePreference?.ToString() ?? "auto", PrimaryColor, AccentColor);
            ThemeChanged?.Invoke();
        }
    }
}
