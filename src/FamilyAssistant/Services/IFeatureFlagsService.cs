namespace FamilyAssistant.Services;

public interface IFeatureFlagsService
{
    Task<bool> IsEnabledAsync(string key, bool defaultValue = false, CancellationToken ct = default);
    Task SetEnabledAsync(string key, bool enabled, CancellationToken ct = default);

    Task<bool> IsRecipesEnabledAsync(CancellationToken ct = default);
    Task SetRecipesEnabledAsync(bool enabled, CancellationToken ct = default);
}
