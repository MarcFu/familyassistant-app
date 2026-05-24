namespace FamilyAssist.Services;

/// <summary>
/// Background service that establishes the HA WebSocket connection on startup
/// and reads the initial theme preference.
/// </summary>
public class HaWebSocketStartupService : BackgroundService
{
    private readonly IHomeAssistantService _haService;
    private readonly HaThemeService _themeService;
    private readonly ILogger<HaWebSocketStartupService> _logger;

    public HaWebSocketStartupService(
        IHomeAssistantService haService,
        HaThemeService themeService,
        ILogger<HaWebSocketStartupService> logger)
    {
        _haService = haService;
        _themeService = themeService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Retry loop for WebSocket connection
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Establishing HA WebSocket connection...");
                await _haService.ConnectWebSocketAsync(stoppingToken);

                if (_haService.IsWebSocketConnected)
                {
                    _logger.LogInformation("HA WebSocket connected, reading theme...");
                    await _themeService.RefreshAsync(stoppingToken);
                    break; // Success — exit retry loop
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect HA WebSocket, retrying in 30s...");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        // Periodically refresh theme (every 5 min) in case user changes it in HA
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            if (_haService.IsWebSocketConnected)
            {
                await _themeService.RefreshAsync(stoppingToken);
            }
        }
    }
}
