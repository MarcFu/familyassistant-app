using System.Reflection;
using MudBlazor;

namespace FamilyAssist.Services;

/// <summary>
/// Lazy-cached registry of all MudBlazor Material Design icons (Filled variant).
/// Uses reflection once, then serves from memory.
/// </summary>
public static class IconRegistry
{
    private static readonly Lazy<Dictionary<string, string>> _icons = new(LoadIcons);
    private static readonly Lazy<List<IconEntry>> _entries = new(() =>
        _icons.Value.Select(kv => new IconEntry(kv.Key, kv.Value)).ToList());

    /// <summary>
    /// All icon names (e.g., "Home", "CleaningServices", "Kitchen").
    /// </summary>
    public static IReadOnlyDictionary<string, string> All => _icons.Value;

    /// <summary>
    /// All icons as entries (for enumeration/display).
    /// </summary>
    public static IReadOnlyList<IconEntry> Entries => _entries.Value;

    /// <summary>
    /// Get the SVG path string for a given icon name.
    /// </summary>
    public static string? GetSvg(string name)
        => _icons.Value.TryGetValue(name, out var svg) ? svg : null;

    /// <summary>
    /// Check if an icon name exists.
    /// </summary>
    public static bool Exists(string name) => _icons.Value.ContainsKey(name);

    private static Dictionary<string, string> LoadIcons()
    {
        var fields = typeof(Icons.Material.Filled)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && !f.Name.StartsWith("_"));

        var dict = new Dictionary<string, string>(2200);
        foreach (var field in fields)
        {
            var value = field.GetValue(null) as string;
            if (value is not null)
            {
                dict[field.Name] = value;
            }
        }
        return dict;
    }
}

/// <summary>
/// Represents a single icon entry with name and SVG path.
/// </summary>
public record IconEntry(string Name, string Svg);
