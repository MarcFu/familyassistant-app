using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyAssistant.Services;

public static partial class RecipeStepIngredientMatcher
{
    private static readonly HashSet<string> IgnoredIngredientTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "frisch",
        "frische",
        "frischer",
        "frisches",
        "gross",
        "grosse",
        "grosser",
        "grosses",
        "groß",
        "große",
        "großer",
        "großes",
        "klein",
        "kleine",
        "kleiner",
        "kleines",
        "optional",
        "oder",
        "und",
        "nach",
        "geschmack"
    };

    public static HashSet<int> ResolveStepIngredientIds(
        IReadOnlyList<string> ingredientNames,
        IEnumerable<string> importedIngredientNames,
        string? instruction)
    {
        var selectedIds = ResolveImportedIngredientIds(ingredientNames, importedIngredientNames);

        if (!string.IsNullOrWhiteSpace(instruction))
        {
            for (var index = 0; index < ingredientNames.Count; index++)
            {
                if (InstructionMentionsIngredient(instruction, ingredientNames[index]))
                {
                    selectedIds.Add(index + 1);
                }
            }
        }

        return selectedIds;
    }

    private static HashSet<int> ResolveImportedIngredientIds(IReadOnlyList<string> ingredientNames, IEnumerable<string> importedIngredientNames)
    {
        var selectedIds = new HashSet<int>();
        foreach (var importedName in importedIngredientNames)
        {
            var normalizedName = NormalizeCompact(importedName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            var exactIndex = FindIngredientIndex(ingredientNames, name => NormalizeCompact(name) == normalizedName);
            if (exactIndex >= 0)
            {
                selectedIds.Add(exactIndex + 1);
                continue;
            }

            var matchingIndexes = ingredientNames
                .Select((ingredientName, index) => new { Name = NormalizeCompact(ingredientName), Index = index })
                .Where(i => !string.IsNullOrWhiteSpace(i.Name)
                    && (i.Name.Contains(normalizedName, StringComparison.OrdinalIgnoreCase)
                        || normalizedName.Contains(i.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(i => i.Index)
                .ToList();
            if (matchingIndexes.Count == 1)
            {
                selectedIds.Add(matchingIndexes[0] + 1);
            }
        }

        return selectedIds;
    }

    private static bool InstructionMentionsIngredient(string instruction, string ingredientName)
    {
        var ingredientCompact = NormalizeCompact(ingredientName);
        if (ingredientCompact.Length >= 4 && NormalizeCompact(instruction).Contains(ingredientCompact, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var instructionTokens = Tokenize(instruction).ToList();
        if (instructionTokens.Count == 0)
        {
            return false;
        }

        var ingredientTokens = Tokenize(ingredientName)
            .Where(token => !IgnoredIngredientTokens.Contains(token))
            .Where(IsDistinctIngredientToken)
            .SelectMany(GetTokenVariants)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var ingredientToken in ingredientTokens)
        {
            if (instructionTokens.Any(instructionToken => TokensMatch(instructionToken, ingredientToken)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TokensMatch(string instructionToken, string ingredientToken)
    {
        if (string.Equals(instructionToken, ingredientToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ingredientToken.Length < 4 || instructionToken.Length < 4)
        {
            return false;
        }

        return ingredientToken.EndsWith(instructionToken, StringComparison.OrdinalIgnoreCase)
            || instructionToken.EndsWith(ingredientToken, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDistinctIngredientToken(string token) => token.Length >= 3 || token is "ei" or "öl" or "oel";

    private static IEnumerable<string> GetTokenVariants(string token)
    {
        yield return token;

        if (token == "eier")
        {
            yield return "ei";
        }

        if (token.Length <= 4)
        {
            yield break;
        }

        foreach (var suffix in new[] { "en", "er", "es", "e", "n", "s" })
        {
            if (token.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && token.Length - suffix.Length >= 3)
            {
                yield return token[..^suffix.Length];
            }
        }
    }

    private static int FindIngredientIndex(IReadOnlyList<string> ingredientNames, Func<string, bool> predicate)
    {
        for (var index = 0; index < ingredientNames.Count; index++)
        {
            if (predicate(ingredientNames[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        foreach (Match match in WordRegex().Matches(NormalizeText(value)))
        {
            yield return match.Value;
        }
    }

    private static string NormalizeCompact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(Tokenize(value));
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
