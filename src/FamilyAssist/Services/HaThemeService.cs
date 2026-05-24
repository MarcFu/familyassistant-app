namespace FamilyAssist.Services;

/// <summary>
/// Reads the HA frontend theme preference and exposes it to the UI.
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
    /// Current dark mode state. Defaults to true (dark) until WebSocket delivers real value.
    /// </summary>
    public bool IsDarkMode { get; private set; } = true;

    /// <summary>
    /// Fired when the theme changes (e.g., after initial load from HA).
    /// </summary>
    public event Action? ThemeChanged;

    /// <summary>
    /// Reads the theme from HA via WebSocket. Called once after WebSocket connects.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var isDark = await _haService.GetUserDarkModeAsync(ct);
            if (isDark != IsDarkMode)
            {
                IsDarkMode = isDark;
                _logger.LogInformation("Theme updated from HA: IsDarkMode={IsDark}", isDark);
                ThemeChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read theme from HA, keeping current: IsDarkMode={IsDark}", IsDarkMode);
        }
    }
}
