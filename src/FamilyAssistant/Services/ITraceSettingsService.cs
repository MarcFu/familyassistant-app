namespace FamilyAssistant.Services;

public interface ITraceSettingsService
{
    Task<bool> IsEnabledAsync(string key, bool defaultValue = false, CancellationToken ct = default);
    Task SetEnabledAsync(string key, bool enabled, CancellationToken ct = default);
    Task<bool> IsRecipeImportTraceEnabledAsync(CancellationToken ct = default);
    Task SetRecipeImportTraceEnabledAsync(bool enabled, CancellationToken ct = default);
}
