using System.Text.RegularExpressions;

namespace FamilyAssistant.Services;

public static partial class RecipeStepMarkupHelper
{
    public static string CreateMarker(string token) => $"{{{{ingredient:{token}}}}}";

    public static IReadOnlyList<RecipeStepMarkupSegment> Parse(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return [];
        }

        var segments = new List<RecipeStepMarkupSegment>();
        var lastIndex = 0;
        foreach (Match match in IngredientMarkerRegex().Matches(markup))
        {
            if (match.Index > lastIndex)
            {
                segments.Add(RecipeStepMarkupSegment.TextSegment(markup[lastIndex..match.Index]));
            }

            var token = match.Groups["token"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                segments.Add(RecipeStepMarkupSegment.Ingredient(token));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < markup.Length)
        {
            segments.Add(RecipeStepMarkupSegment.TextSegment(markup[lastIndex..]));
        }

        return segments;
    }

    public static string StripMarkers(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return string.Empty;
        }

        return NormalizeWhitespace(IngredientMarkerRegex().Replace(markup, string.Empty));
    }

    public static HashSet<string> ExtractTokens(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return [];
        }

        return IngredientMarkerRegex()
            .Matches(markup)
            .Select(match => match.Groups["token"].Value.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static string? CleanMarkup(string? markup, IReadOnlySet<string> validTokens)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return null;
        }

        var cleaned = NormalizeWhitespace(IngredientMarkerRegex().Replace(markup, match =>
        {
            var token = match.Groups["token"].Value.Trim();
            return validTokens.Contains(token) ? CreateMarker(token) : string.Empty;
        })).Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    public static string? ConvertIngredientNameMarkersToTokens(
        string? markup,
        IReadOnlyDictionary<string, string> tokensByIngredientName)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return null;
        }

        var converted = NormalizeWhitespace(IngredientMarkerRegex().Replace(markup, match =>
        {
            var ingredientName = match.Groups["token"].Value.Trim();
            return tokensByIngredientName.TryGetValue(ingredientName, out var token)
                ? CreateMarker(token)
                : string.Empty;
        })).Trim();

        return string.IsNullOrWhiteSpace(converted) ? null : converted;
    }

    public static string RemoveMarkersForToken(string? markup, string token)
    {
        if (string.IsNullOrWhiteSpace(markup) || string.IsNullOrWhiteSpace(token))
        {
            return markup ?? string.Empty;
        }

        return IngredientMarkerRegex().Replace(markup, match =>
            string.Equals(match.Groups["token"].Value.Trim(), token, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : match.Value);
    }

    public static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex()
            .Replace(value, " ")
            .Replace(" ,", ",", StringComparison.Ordinal)
            .Replace(" .", ".", StringComparison.Ordinal)
            .Replace(" ;", ";", StringComparison.Ordinal)
            .Replace(" :", ":", StringComparison.Ordinal)
            .Trim();
    }

    [GeneratedRegex(@"\{\{ingredient:(?<token>[^}]+)\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex IngredientMarkerRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}

public sealed record RecipeStepMarkupSegment(string? Text, string? IngredientToken)
{
    public bool IsIngredient => !string.IsNullOrWhiteSpace(IngredientToken);

    public static RecipeStepMarkupSegment TextSegment(string text) => new(text, null);

    public static RecipeStepMarkupSegment Ingredient(string token) => new(null, token);
}
