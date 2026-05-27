namespace FamilyAssistant.Services;

/// <summary>
/// Background service that establishes the HA WebSocket connection on startup.
/// Required for: event triggers (HaEventTriggerService), internet enforcement,
/// event monitoring (Dev page), state subscriptions.
/// </summary>
public class HaWebSocketStartupService : BackgroundService
{
    private readonly IHomeAssistantService _haService;
    private readonly ILogger<HaWebSocketStartupService> _logger;

    public HaWebSocketStartupService(
        IHomeAssistantService haService,
        ILogger<HaWebSocketStartupService> logger)
    {
        _haService = haService;
        _logger = logger;
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
                    _logger.LogInformation("HA WebSocket connected successfully");
                    break; // Success — exit retry loop
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect HA WebSocket, retrying in 30s...");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        // Keep service alive (required for BackgroundService lifetime)
        // The WebSocket connection is maintained by HomeAssistantService internally.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
