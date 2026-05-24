namespace FamilyAssist.Services;

/// <summary>
/// Background service that periodically triggers task generation.
/// </summary>
public class TaskGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskGenerationService> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public TaskGenerationService(IServiceScopeFactory scopeFactory, ILogger<TaskGenerationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<TaskGenerator>();
                await generator.RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task generation cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
