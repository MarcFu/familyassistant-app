using FamilyAssistant.Data;
using FamilyAssistant.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Localization;

/// <summary>
/// Determines the app language. Priority:
/// 1. HA user preference (from HaThemeService, synced via WebSocket)
/// 2. Manual override in AppSettings DB table
/// 3. Fallback: "en"
/// </summary>
public class AppSettingsCultureProvider : RequestCultureProvider
{
    public const string SettingKey = "Language";
    public const string DefaultLanguage = "en";

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // Priority 1: HA language (already resolved by WebSocket background service)
        var themeService = httpContext.RequestServices.GetRequiredService<HaThemeService>();
        if (!string.IsNullOrEmpty(themeService.Language))
        {
            var haLang = NormalizeLanguageCode(themeService.Language);
            return new ProviderCultureResult(haLang, haLang);
        }

        // Priority 2: Manual DB override
        var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
        var setting = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingKey);

        var lang = setting?.Value ?? DefaultLanguage;
        return new ProviderCultureResult(lang, lang);
    }

    /// <summary>
    /// HA may return full locale codes like "de" or region-specific like "de-DE".
    /// We normalize to the base language code supported by our resource files.
    /// </summary>
    private static string NormalizeLanguageCode(string haLanguage)
    {
        // Our app supports: "de", "en", "fr"
        // HA may send: "de", "en", "fr", "en-GB", etc.
        var baseLang = haLanguage.Split('-')[0].ToLowerInvariant();
        return baseLang switch
        {
            "de" => "de",
            "fr" => "fr",
            _ => "en" // Fallback for unsupported languages
        };
    }
}
