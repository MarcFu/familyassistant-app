namespace FamilyAssistant.Services;

/// <summary>
/// Background service that establishes the HA WebSocket connection on startup
/// and refreshes theme/language on every (re)connect.
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

        // Subscribe to (re)connect events — always refresh theme when WebSocket comes up
        _haService.WebSocketConnected += OnWebSocketConnected;
    }

    private void OnWebSocketConnected()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("WebSocket connected, refreshing theme/language...");
                await _themeService.RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh theme after WebSocket connect");
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        // Retry loop for WebSocket connection
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Establishing HA WebSocket connection...");
                await _haService.ConnectWebSocketAsync(stoppingToken);

                if (_haService.IsWebSocketConnected)
                {
                    break; // Success — exit retry loop (OnWebSocketConnected handles refresh)
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect HA WebSocket, retrying in 30s...");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        // Keep running: periodically refresh theme (every 5 min) in case user changes it in HA
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            if (_haService.IsWebSocketConnected)
            {
                try
                {
                    await _themeService.RefreshAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Periodic theme refresh failed");
                }
            }
        }
    }
}
