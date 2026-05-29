using System.Text.Json;

namespace FamilyAssistant.Services;

/// <summary>
/// Reads per-user frontend preferences (language, theme, colors) directly from
/// Home Assistant's config storage files at /config/.storage/frontend.user_data_{userId}.
/// This bypasses the WebSocket API limitation where SUPERVISOR_TOKEN cannot read other users' data.
/// </summary>
public class HaUserPreferencesService
{
    private readonly ILogger<HaUserPreferencesService> _logger;
    private readonly string _configBasePath;
    private readonly Dictionary<string, CachedPreferences> _cache = new();
    private readonly object _lock = new();

    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public HaUserPreferencesService(ILogger<HaUserPreferencesService> logger)
    {
        _logger = logger;

        // HA add-on config directory is mounted at /config (via map: config:ro in config.yaml)
        // In development (Windows), this won't exist — service returns null gracefully.
        _configBasePath = Environment.GetEnvironmentVariable("HA_CONFIG_PATH") ?? "/config";
    }

    /// <summary>
    /// Gets cached preferences for a user. Returns null if not available or file doesn't exist.
    /// </summary>
    public UserPreferences? GetPreferences(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        lock (_lock)
        {
            if (_cache.TryGetValue(userId, out var cached) && !cached.IsExpired)
                return cached.Preferences;
        }

        // Cache miss or expired — try to read from file
        return ReadAndCache(userId);
    }

    /// <summary>
    /// Forces a re-read from the file system for a specific user.
    /// </summary>
    public UserPreferences? Refresh(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        return ReadAndCache(userId);
    }

    /// <summary>
    /// Gets the file path that would be read for a given user ID (for diagnostics).
    /// </summary>
    public string GetFilePath(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return "(no user ID)";
        return Path.Combine(_configBasePath, ".storage", $"frontend.user_data_{userId}");
    }

    /// <summary>
    /// Gets the raw JSON content of the user's preference file (for diagnostics).
    /// Returns null if file doesn't exist.
    /// </summary>
    public string? GetRawFileContent(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        var filePath = GetFilePath(userId);
        try
        {
            if (File.Exists(filePath))
                return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read raw file content for diagnostics: {Path}", filePath);
        }
        return null;
    }

    /// <summary>
    /// Diagnostics: last read status per user.
    /// </summary>
    public string? GetLastStatus(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        lock (_lock)
        {
            return _cache.TryGetValue(userId, out var cached) ? cached.Status : null;
        }
    }

    /// <summary>
    /// Diagnostics: last read timestamp per user.
    /// </summary>
    public DateTime? GetLastReadTime(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        lock (_lock)
        {
            return _cache.TryGetValue(userId, out var cached) ? cached.ReadAt : null;
        }
    }

    private UserPreferences? ReadAndCache(string userId)
    {
        var filePath = GetFilePath(userId);
        string status;
        UserPreferences? prefs = null;

        try
        {
            if (!File.Exists(filePath))
            {
                status = $"File not found: {filePath}";
                _logger.LogDebug("User preferences file not found: {Path}", filePath);
            }
            else
            {
                var json = File.ReadAllText(filePath);
                prefs = ParsePreferencesFile(json);
                status = prefs is not null
                    ? $"OK (lang={prefs.Language ?? "null"}, dark={prefs.DarkMode?.ToString() ?? "auto"})"
                    : "Parsed but no usable data found";

                _logger.LogInformation(
                    "Read user preferences for {UserId}: lang={Lang}, dark={Dark}, primary={Primary}, accent={Accent}",
                    userId, prefs?.Language, prefs?.DarkMode?.ToString() ?? "auto",
                    prefs?.PrimaryColor, prefs?.AccentColor);
            }
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
            _logger.LogWarning(ex, "Failed to read user preferences file: {Path}", filePath);
        }

        lock (_lock)
        {
            _cache[userId] = new CachedPreferences(prefs, DateTime.Now, status);
        }

        return prefs;
    }

    /// <summary>
    /// Parses the HA storage file format:
    /// {
    ///   "version": 1,
    ///   "key": "frontend.user_data_xxx",
    ///   "data": {
    ///     "language": { "language": "de" },
    ///     "core": { "showAdvanced": true },
    ///     "selectedTheme": { "theme": "default", "dark": true, "primaryColor": "#03a9f4", "accentColor": "#ff9800" }
    ///   }
    /// }
    /// </summary>
    private UserPreferences? ParsePreferencesFile(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;

            string? language = null;
            bool? darkMode = null;
            string? primaryColor = null;
            string? accentColor = null;

            // Language: stored under data.language.language
            if (data.TryGetProperty("language", out var langObj) && langObj.ValueKind == JsonValueKind.Object)
            {
                if (langObj.TryGetProperty("language", out var langVal) && langVal.ValueKind == JsonValueKind.String)
                {
                    language = langVal.GetString();
                }
            }

            // Theme: stored under data.theme (current HA format), fallback data.selectedTheme (older versions)
            var themeSource = data;
            if (data.TryGetProperty("theme", out var themeObj) && themeObj.ValueKind == JsonValueKind.Object)
            {
                themeSource = themeObj;
            }
            else if (data.TryGetProperty("selectedTheme", out var selectedTheme) && selectedTheme.ValueKind == JsonValueKind.Object)
            {
                themeSource = selectedTheme;
            }

            // Dark mode
            if (themeSource.TryGetProperty("dark", out var dark))
            {
                if (dark.ValueKind == JsonValueKind.True)
                    darkMode = true;
                else if (dark.ValueKind == JsonValueKind.False)
                    darkMode = false;
                // null/missing = auto
            }

            // Colors
            if (themeSource.TryGetProperty("primaryColor", out var pc) && pc.ValueKind == JsonValueKind.String)
            {
                primaryColor = pc.GetString();
            }
            if (themeSource.TryGetProperty("accentColor", out var ac) && ac.ValueKind == JsonValueKind.String)
            {
                accentColor = ac.GetString();
            }

            return new UserPreferences(language, darkMode, primaryColor, accentColor);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse user preferences JSON");
            return null;
        }
    }

    // ─── Inner types ─────────────────────────────────────────────────

    private record CachedPreferences(UserPreferences? Preferences, DateTime ReadAt, string Status)
    {
        public bool IsExpired => DateTime.Now - ReadAt > CacheExpiry;
    }
}

/// <summary>
/// Per-user frontend preferences read from HA config storage.
/// </summary>
public record UserPreferences(
    string? Language,
    bool? DarkMode,
    string? PrimaryColor,
    string? AccentColor
);
