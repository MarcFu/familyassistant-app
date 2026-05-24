using System.Xml.Linq;

namespace FamilyAssist.Tests;

/// <summary>
/// Ensures all translation resource files have the same keys.
/// Catches missing translations at build/test time instead of runtime.
/// </summary>
public class ResourceCompletenessTests
{
    private static readonly string ResourceDir = Path.Combine(
        FindSolutionRoot(), "src", "FamilyAssist", "Resources");

    private static readonly string[] SupportedLanguages = ["de", "en", "fr"];

    [Fact]
    public void AllLanguages_HaveSameKeys()
    {
        var keysByLanguage = new Dictionary<string, HashSet<string>>();

        foreach (var lang in SupportedLanguages)
        {
            var filePath = Path.Combine(ResourceDir, $"SharedResource.{lang}.resx");
            Assert.True(File.Exists(filePath), $"Resource file missing: {filePath}");

            var keys = ReadResxKeys(filePath);
            keysByLanguage[lang] = keys;
        }

        // Use German as the reference (primary language)
        var referenceKeys = keysByLanguage["de"];
        var allErrors = new List<string>();

        foreach (var lang in SupportedLanguages)
        {
            if (lang == "de") continue;

            var langKeys = keysByLanguage[lang];

            var missingInLang = referenceKeys.Except(langKeys).OrderBy(k => k).ToList();
            var extraInLang = langKeys.Except(referenceKeys).OrderBy(k => k).ToList();

            foreach (var key in missingInLang)
            {
                allErrors.Add($"[{lang}] MISSING key: \"{key}\"");
            }

            foreach (var key in extraInLang)
            {
                allErrors.Add($"[{lang}] EXTRA key (not in de): \"{key}\"");
            }
        }

        if (allErrors.Count > 0)
        {
            Assert.Fail($"Resource completeness check failed ({allErrors.Count} issue(s)):\n" +
                        string.Join("\n", allErrors));
        }
    }

    [Fact]
    public void AllLanguages_HaveNoEmptyValues()
    {
        var errors = new List<string>();

        foreach (var lang in SupportedLanguages)
        {
            var filePath = Path.Combine(ResourceDir, $"SharedResource.{lang}.resx");
            var entries = ReadResxEntries(filePath);

            foreach (var (key, value) in entries)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"[{lang}] EMPTY value for key: \"{key}\"");
                }
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail($"Found {errors.Count} empty translation(s):\n" +
                        string.Join("\n", errors));
        }
    }

    [Fact]
    public void AllLanguages_HaveNoDuplicateKeys()
    {
        var errors = new List<string>();

        foreach (var lang in SupportedLanguages)
        {
            var filePath = Path.Combine(ResourceDir, $"SharedResource.{lang}.resx");
            var doc = XDocument.Load(filePath);
            var keys = doc.Root!.Elements("data")
                .Select(e => e.Attribute("name")?.Value)
                .Where(k => k is not null)
                .Cast<string>()
                .ToList();

            var duplicates = keys.GroupBy(k => k)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var dup in duplicates)
            {
                errors.Add($"[{lang}] DUPLICATE key: \"{dup}\"");
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail($"Found {errors.Count} duplicate key(s):\n" +
                        string.Join("\n", errors));
        }
    }

    // ─── Helpers ────────────────────────────────────────────────

    private static HashSet<string> ReadResxKeys(string filePath)
    {
        var doc = XDocument.Load(filePath);
        return doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(k => k is not null)
            .Cast<string>()
            .ToHashSet();
    }

    private static List<(string Key, string? Value)> ReadResxEntries(string filePath)
    {
        var doc = XDocument.Load(filePath);
        return doc.Root!.Elements("data")
            .Select(e => (
                Key: e.Attribute("name")?.Value ?? "",
                Value: e.Element("value")?.Value
            ))
            .Where(e => e.Key != "")
            .ToList();
    }

    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: navigate from test binary location
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
