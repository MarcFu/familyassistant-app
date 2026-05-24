using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FamilyAssist.Localization;

/// <summary>
/// Bridges MudBlazor's internal localization (table pagination, date pickers, etc.)
/// to our shared resource file.
/// MudBlazor keys are prefixed with "MudBlazor." in the .resx files.
/// </summary>
public class AppMudLocalizer : MudLocalizer
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AppMudLocalizer(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public override LocalizedString this[string key]
    {
        get
        {
            // Try with MudBlazor prefix first
            var result = _localizer[$"MudBlazor.{key}"];
            if (!result.ResourceNotFound)
                return result;

            // Fallback to key as-is (MudBlazor will use its built-in English)
            return new LocalizedString(key, key, resourceNotFound: true);
        }
    }
}
