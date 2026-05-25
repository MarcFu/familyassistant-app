using FamilyAssistant.Data;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Localization;

/// <summary>
/// Reads the app language from the AppSettings DB table.
/// Falls back to "en" if no setting exists.
/// Key: "Language", Value: ISO culture code (e.g., "de", "en").
/// </summary>
public class AppSettingsCultureProvider : RequestCultureProvider
{
    public const string SettingKey = "Language";
    public const string DefaultLanguage = "en";

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
        var setting = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingKey);

        var lang = setting?.Value ?? DefaultLanguage;
        return new ProviderCultureResult(lang, lang);
    }
}
