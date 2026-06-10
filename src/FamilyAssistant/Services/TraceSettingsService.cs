using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

public class TraceSettingsService(AppDbContext db) : ITraceSettingsService
{
    public async Task<bool> IsEnabledAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var value = await db.AppSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        return bool.TryParse(value, out var enabled) ? enabled : defaultValue;
    }

    public async Task SetEnabledAsync(string key, bool enabled, CancellationToken ct = default)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = enabled.ToString()
            });
        }
        else
        {
            setting.Value = enabled.ToString();
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsRecipeImportTraceEnabledAsync(CancellationToken ct = default)
        => IsEnabledAsync(TraceSettingKeys.RecipeImportEnabled, ct: ct);

    public Task SetRecipeImportTraceEnabledAsync(bool enabled, CancellationToken ct = default)
        => SetEnabledAsync(TraceSettingKeys.RecipeImportEnabled, enabled, ct);
}
