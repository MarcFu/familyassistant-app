using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using FamilyAssistant.Models;

namespace FamilyAssistant.Services;

public enum RecipeImportStage
{
    ValidateUrl,
    DownloadPage,
    FindRecipeData,
    ParseMetadata,
    ParseIngredients,
    ParseSteps,
    PrepareDraft,
    AiExtraction,
    AiValidation,
    AiRefinement,
    FinalValidation,
    Completed
}

public record RecipeImportProgress(int Percent, string Message, RecipeImportStage Stage);

public class RecipeImportService(HttpClient httpClient, ILogger<RecipeImportService> logger, RecipeAiImportEnhancer aiEnhancer, RecipeImportTraceService traceService)
{
    private static readonly Regex JsonLdScriptRegex = new(
        "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ServingsNumberRegex = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex LeadingDecorationRegex = new(@"^[^\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex IngredientRegex = new(
        @"^\s*(?<quantity>\d+(?:[\.,]\d+)?(?:\s*/\s*\d+(?:[\.,]\d+)?)?(?:\s*[-–]\s*\d+(?:[\.,]\d+)?)?)?\s*(?<unit>(?:g|kg|ml|l|el|tl|gestr\.\s*tl|esslöffel|teelöffel|dose|dosen|zehe|zehen|prise|prisen|stück|stk\.?|cup|cups|tbsp|tsp)(?!\p{L}))?\s*(?<name>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> RecipeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Recipe",
        "schema:Recipe"
    };

    public async Task<RecipeImportDraft> ImportFromUrlAsync(
        string url,
        RecipeImportQuality quality = RecipeImportQuality.SimpleParsing,
        string? targetLanguage = null,
        IProgress<RecipeImportProgress>? progress = null,
        RecipeImportTraceContext? trace = null,
        CancellationToken ct = default)
    {
        progress?.Report(new RecipeImportProgress(5, "URL prüfen", RecipeImportStage.ValidateUrl));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Only absolute HTTP/HTTPS URLs are supported.");
        }

        progress?.Report(new RecipeImportProgress(15, "Seite abrufen", RecipeImportStage.DownloadPage));
        var html = await traceService.TraceAsync(
            trace,
            "FetchSource",
            RecipeImportStage.DownloadPage.ToString(),
            new { url = uri.ToString() },
            async () =>
            {
                using var response = await httpClient.GetAsync(uri, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct);
            },
            output: result => new { html = RecipeImportTraceService.TracedText(result) });
        ct.ThrowIfCancellationRequested();

        progress?.Report(new RecipeImportProgress(35, "Rezeptdaten suchen", RecipeImportStage.FindRecipeData));
        var draft = await traceService.TraceAsync(
            trace,
            "DeterministicParse",
            RecipeImportStage.ParseMetadata.ToString(),
            new { sourceUrl = uri.ToString(), html = RecipeImportTraceService.TracedText(html) },
            () =>
            {
                var recipeNode = FindRecipeJsonLd(html);
                progress?.Report(new RecipeImportProgress(55, "Rezeptdaten auslesen", RecipeImportStage.ParseMetadata));
                var parsedDraft = recipeNode is null
                    ? ParseRecipeHtml(html, uri)
                    : ParseRecipe(recipeNode.Value, uri);
                return Task.FromResult(parsedDraft);
            },
            output: parsedDraft => ToTraceDraft(parsedDraft));
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            throw new InvalidOperationException("Recipe title could not be parsed.");
        }

        progress?.Report(new RecipeImportProgress(75, "Zutaten auslesen", RecipeImportStage.ParseIngredients));
        ct.ThrowIfCancellationRequested();
        progress?.Report(new RecipeImportProgress(90, "Schritte auslesen", RecipeImportStage.ParseSteps));
        ct.ThrowIfCancellationRequested();
        if (quality != RecipeImportQuality.SimpleParsing)
        {
            progress?.Report(new RecipeImportProgress(95, "AI verbessert Import", RecipeImportStage.PrepareDraft));
            draft = await aiEnhancer.EnhanceAsync(draft, html, quality, targetLanguage, progress, trace, ct);
        }
        ValidateDraft(draft);
        await traceService.TracePointAsync(
            trace,
            "FinalValidation",
            RecipeImportStage.FinalValidation.ToString(),
            null,
            ToTraceDraft(draft));

        progress?.Report(new RecipeImportProgress(100, "Import-Vorschau bereit", RecipeImportStage.Completed));
        return draft;
    }

    public async Task<RecipeImportDraft> ImportFromTextAsync(
        string text,
        RecipeSourceType sourceType = RecipeSourceType.Text,
        RecipeImportQuality quality = RecipeImportQuality.SimpleParsing,
        string? targetLanguage = null,
        IProgress<RecipeImportProgress>? progress = null,
        RecipeImportTraceContext? trace = null,
        CancellationToken ct = default)
    {
        progress?.Report(new RecipeImportProgress(10, "Text prüfen", RecipeImportStage.ValidateUrl));
        ct.ThrowIfCancellationRequested();

        var draft = await traceService.TraceAsync(
            trace,
            "DeterministicParse",
            RecipeImportStage.ParseMetadata.ToString(),
            new { sourceType, text = RecipeImportTraceService.TracedText(text) },
            () => Task.FromResult(ParseText(text, sourceType)),
            output: parsedDraft => ToTraceDraft(parsedDraft));
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            throw new InvalidOperationException("Recipe title could not be parsed.");
        }

        progress?.Report(new RecipeImportProgress(55, "Rezepttext auslesen", RecipeImportStage.ParseMetadata));
        ct.ThrowIfCancellationRequested();
        progress?.Report(new RecipeImportProgress(75, "Zutaten auslesen", RecipeImportStage.ParseIngredients));
        ct.ThrowIfCancellationRequested();
        progress?.Report(new RecipeImportProgress(90, "Schritte auslesen", RecipeImportStage.ParseSteps));
        ct.ThrowIfCancellationRequested();
        if (quality != RecipeImportQuality.SimpleParsing)
        {
            progress?.Report(new RecipeImportProgress(95, "AI verbessert Import", RecipeImportStage.PrepareDraft));
            draft = await aiEnhancer.EnhanceAsync(draft, text, quality, targetLanguage, progress, trace, ct);
        }
        ValidateDraft(draft);
        await traceService.TracePointAsync(
            trace,
            "FinalValidation",
            RecipeImportStage.FinalValidation.ToString(),
            null,
            ToTraceDraft(draft));

        progress?.Report(new RecipeImportProgress(100, "Import-Vorschau bereit", RecipeImportStage.Completed));

        return draft;
    }

    private static void ValidateDraft(RecipeImportDraft draft)
    {
        draft.Name = draft.Name.Trim();
        draft.CategoryName = string.IsNullOrWhiteSpace(draft.CategoryName) ? null : draft.CategoryName.Trim();
        draft.Tags = draft.Tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        draft.Ingredients = draft.Ingredients
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i =>
            {
                i.Name = i.Name.Trim();
                i.Unit = string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit.Trim();
                i.Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim();
                i.GroupName = string.IsNullOrWhiteSpace(i.GroupName) ? null : i.GroupName.Trim();
                return i;
            })
            .ToList();
        var ingredientNames = draft.Ingredients.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        draft.Steps = draft.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
            .Select(s =>
            {
                s.Instruction = s.Instruction.Trim();
                s.InstructionMarkup = string.IsNullOrWhiteSpace(s.InstructionMarkup) ? null : s.InstructionMarkup.Trim();
                s.Section = string.IsNullOrWhiteSpace(s.Section) ? null : s.Section.Trim();
                s.GroupNumber = s.GroupNumber <= 0 ? 1 : s.GroupNumber;
                s.IngredientNames = s.IngredientNames
                    .Where(name => ingredientNames.Contains(name.Trim()))
                    .Select(name => ingredientNames.First(existing => string.Equals(existing, name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return s;
            })
            .ToList();
    }

    private static object ToTraceDraft(RecipeImportDraft draft) => new
    {
        draft.Name,
        draft.Description,
        draft.Notes,
        draft.BaseServings,
        draft.ImageUrl,
        draft.SourceUrl,
        draft.SourceName,
        draft.SourceType,
        draft.CategoryName,
        draft.Tags,
        draft.TotalTimeMinutes,
        draft.WorkTimeMinutes,
        draft.CookTimeMinutes,
        draft.RestTimeMinutes,
        draft.Difficulty,
        draft.QualityReport,
        Ingredients = draft.Ingredients.Select(i => new { i.Quantity, i.Unit, i.Name, i.Note, i.OriginalText, i.GroupName }),
        Steps = draft.Steps.Select(s => new { s.Instruction, s.InstructionMarkup, s.Section, s.GroupNumber, s.ImageUrl, s.IngredientNames })
    };

    public static RecipeImportDraft ParseJsonLdForTests(string jsonLd, Uri sourceUri)
    {
        using var doc = JsonDocument.Parse(jsonLd);
        var recipeNode = FindRecipeElement(doc.RootElement)
            ?? throw new InvalidOperationException("No schema.org Recipe JSON-LD block found.");

        return ParseRecipe(recipeNode, sourceUri);
    }

    public static RecipeImportDraft ParseHtmlMicrodataForTests(string html, Uri sourceUri) => ParseRecipeHtml(html, sourceUri);

    public static RecipeImportDraft ParseTextForTests(string text, RecipeSourceType sourceType = RecipeSourceType.Text) => ParseText(text, sourceType);

    private JsonElement? FindRecipeJsonLd(string html)
    {
        foreach (Match match in JsonLdScriptRegex.Matches(html))
        {
            var json = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var recipe = FindRecipeElement(doc.RootElement);
                if (recipe is not null)
                {
                    return recipe.Value.Clone();
                }
            }
            catch (JsonException ex)
            {
                logger.LogDebug(ex, "Skipping invalid JSON-LD block while importing recipe.");
            }
        }

        return null;
    }

    private static JsonElement? FindRecipeElement(JsonElement element)
    {
        if (IsRecipe(element))
        {
            return element;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var result = FindRecipeElement(item);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@graph", out var graph))
            {
                var result = FindRecipeElement(graph);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static bool IsRecipe(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return RecipeTypes.Contains(typeElement.GetString() ?? "");
        }

        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            return typeElement.EnumerateArray()
                .Where(t => t.ValueKind == JsonValueKind.String)
                .Any(t => RecipeTypes.Contains(t.GetString() ?? ""));
        }

        return false;
    }

    private static RecipeImportDraft ParseRecipe(JsonElement recipeNode, Uri sourceUri)
    {
        var draft = new RecipeImportDraft
        {
            Name = ReadString(recipeNode, "name") ?? "",
            Description = ReadString(recipeNode, "description"),
            BaseServings = ParseServings(ReadString(recipeNode, "recipeYield")) ?? 4,
            ImageUrl = ResolveUrl(ReadImage(recipeNode), sourceUri),
            SourceUrl = ReadString(recipeNode, "mainEntityOfPage") ?? sourceUri.ToString(),
            SourceName = sourceUri.Host,
            SourceType = RecipeSourceType.Url,
            CategoryName = ReadString(recipeNode, "recipeCategory"),
            Tags = ReadKeywords(recipeNode).ToList(),
            WorkTimeMinutes = ParseDurationMinutes(ReadString(recipeNode, "prepTime")),
            CookTimeMinutes = ParseDurationMinutes(ReadString(recipeNode, "cookTime")),
            TotalTimeMinutes = ParseDurationMinutes(ReadString(recipeNode, "totalTime")),
            RestTimeMinutes = ParseDurationMinutes(ReadString(recipeNode, "restTime")),
            Difficulty = ReadString(recipeNode, "difficulty")
        };

        if (recipeNode.TryGetProperty("recipeIngredient", out var ingredientElement))
        {
            draft.Ingredients = ReadStringArray(ingredientElement)
                .Select(ParseIngredient)
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .ToList();
        }

        if (recipeNode.TryGetProperty("recipeInstructions", out var instructionsElement))
        {
            draft.Steps = ReadInstructions(instructionsElement, sourceUri)
                .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
                .ToList();
        }

        return draft;
    }

    private static RecipeImportDraft ParseRecipeHtml(string html, Uri sourceUri)
    {
        var draft = new RecipeImportDraft
        {
            Name = ReadItemPropText(html, "name") ?? ReadFirstHeading(html) ?? ReadTitle(html) ?? "",
            Description = ReadItemPropText(html, "description") ?? ReadMetaContent(html, "description"),
            Notes = ReadItemPropText(html, "recipeHint"),
            BaseServings = ParseServings(ReadItemPropText(html, "recipeYield")) is { } servings && servings > 0 ? servings : 4,
            ImageUrl = ResolveUrl(ReadItemPropText(html, "image") ?? ReadOpenGraphContent(html, "og:image"), sourceUri),
            SourceUrl = sourceUri.ToString(),
            SourceName = sourceUri.Host,
            SourceType = RecipeSourceType.Url,
            CategoryName = ReadItemPropText(html, "recipeCategory"),
            WorkTimeMinutes = ParseDurationMinutes(ReadItemPropText(html, "prepTime")),
            CookTimeMinutes = ParseDurationMinutes(ReadItemPropText(html, "cookTime")),
            TotalTimeMinutes = ParseDurationMinutes(ReadItemPropText(html, "totalTime")),
            Difficulty = ReadDifficulty(html)
        };

        draft.Ingredients = ReadRepeatedItemProp(html, "recipeIngredient")
            .Select(ParseIngredient)
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .ToList();

        draft.Steps = ReadRepeatedItemProp(html, "text", preserveLineBreaks: true)
            .SelectMany(SplitInstructionText)
            .Select((instruction, index) => new RecipeStepDraft
            {
                Instruction = instruction,
                GroupNumber = 1
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
            .ToList();

        if (draft.Ingredients.Count == 0 || draft.Steps.Count == 0)
        {
            ApplyMediaWikiRecipeFallback(draft, html, sourceUri);
        }

        if (string.IsNullOrWhiteSpace(draft.Name) || draft.Ingredients.Count == 0 || draft.Steps.Count == 0)
        {
            throw new InvalidOperationException("No supported schema.org Recipe data found.");
        }

        return draft;
    }

    private static void ApplyMediaWikiRecipeFallback(RecipeImportDraft draft, string html, Uri sourceUri)
    {
        draft.Name = ReadFirstHeading(html) ?? CleanPageTitle(draft.Name);
        draft.ImageUrl ??= ResolveUrl(ReadOpenGraphContent(html, "og:image") ?? ReadRecipeTableImage(html), sourceUri);
        draft.BaseServings = ReadRecipeInfoValue(html, "Zutatenmenge für:") is { } servingsText
            && ParseServings(servingsText) is { } servings
            && servings > 0
                ? servings
                : draft.BaseServings;
        draft.TotalTimeMinutes ??= ParseDurationMinutes(ReadRecipeInfoValue(html, "Zeitbedarf:"));
        draft.Difficulty ??= ReadRecipeDifficulty(html);

        var categories = ReadMediaWikiCategories(html).ToList();
        if (categories.Count > 0)
        {
            draft.Tags = categories;
            draft.CategoryName ??= categories.FirstOrDefault(c => c.EndsWith("Küche", StringComparison.OrdinalIgnoreCase))
                ?? categories.FirstOrDefault(c => !c.StartsWith("Rezepte", StringComparison.OrdinalIgnoreCase));
        }

        if (draft.Ingredients.Count == 0 && ReadSectionHtml(html, "Zutaten") is { } ingredientsHtml)
        {
            draft.Ingredients = ReadGroupedListItems(ingredientsHtml)
                .Select(item =>
                {
                    var ingredient = ParseIngredient(item.Text);
                    ingredient.GroupName = item.GroupName;
                    return ingredient;
                })
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .ToList();
        }

        if (draft.Steps.Count == 0 && ReadSectionHtml(html, "Zubereitung") is { } stepsHtml)
        {
            draft.Steps = ReadInstructionSections(stepsHtml)
                .Select(item => new RecipeStepDraft
                {
                    Instruction = item.Text,
                    Section = item.GroupName,
                    GroupNumber = 1
                })
                .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
                .ToList();
        }

        var notes = new[] { "Tipp", "Beilagen", "Varianten" }
            .Select(ReadNotesSection)
            .Where(note => !string.IsNullOrWhiteSpace(note));
        var combinedNotes = string.Join("\n", notes);
        if (!string.IsNullOrWhiteSpace(combinedNotes))
        {
            draft.Notes = string.IsNullOrWhiteSpace(draft.Notes)
                ? combinedNotes
                : $"{draft.Notes}\n{combinedNotes}";
        }

        string? ReadNotesSection(string id) => ReadSectionHtml(html, id) is { } section
            ? string.Join("\n", ReadFlatListItems(section))
            : null;
    }

    private static RecipeImportDraft ParseText(string text, RecipeSourceType sourceType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Recipe text is empty.");
        }

        var rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var lines = rawLines
            .Select(NormalizeTextLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var draft = new RecipeImportDraft
        {
            SourceName = sourceType == RecipeSourceType.WhatsApp ? "WhatsApp" : "Textimport",
            SourceType = sourceType,
            SourceText = text.Trim()
        };

        var section = TextRecipeSection.Title;
        var notes = new StringBuilder();
        var titleFound = false;

        foreach (var line in lines)
        {
            if (IsSocialNoise(line))
            {
                continue;
            }

            if (TryReadTextSection(line, out var nextSection, out var servings))
            {
                section = nextSection;
                if (servings is not null && servings > 0)
                {
                    draft.BaseServings = servings.Value;
                }
                continue;
            }

            if (!titleFound)
            {
                ApplyTitleLine(draft, line);
                titleFound = true;
                continue;
            }

            switch (section)
            {
                case TextRecipeSection.Ingredients:
                    var ingredientText = line.StartsWith("optional:", StringComparison.OrdinalIgnoreCase)
                        ? line["optional:".Length..].Trim()
                        : line;
                    var ingredient = ParseIngredient(ingredientText);
                    if (line.StartsWith("optional:", StringComparison.OrdinalIgnoreCase))
                    {
                        ingredient.Note = "optional";
                    }
                    if (!string.IsNullOrWhiteSpace(ingredient.Name))
                    {
                        draft.Ingredients.Add(ingredient);
                    }
                    break;
                case TextRecipeSection.Steps:
                    draft.Steps.Add(new RecipeStepDraft
                    {
                        Instruction = TrimStepPrefix(line),
                        GroupNumber = 1
                    });
                    break;
                case TextRecipeSection.Notes:
                    notes.AppendLine(line);
                    break;
                default:
                    draft.Description = string.IsNullOrWhiteSpace(draft.Description)
                        ? line
                        : $"{draft.Description}\n{line}";
                    break;
            }
        }

        draft.Notes = notes.Length == 0 ? null : notes.ToString().Trim();
        draft.Ingredients = draft.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
        draft.Steps = draft.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Instruction)).ToList();

        return draft;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => StripHtml(property.GetString()),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.Array => property.EnumerateArray().Select(ReadScalarString).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            _ => null
        };
    }

    private static string? ReadScalarString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => StripHtml(element.GetString()),
        JsonValueKind.Number => element.GetRawText(),
        _ => null
    };

    private static string? ReadImage(JsonElement element)
    {
        if (!element.TryGetProperty("image", out var image))
        {
            return null;
        }

        if (image.ValueKind == JsonValueKind.String)
        {
            return image.GetString();
        }

        if (image.ValueKind == JsonValueKind.Array)
        {
            return image.EnumerateArray()
                .Select(ReadImageValue)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        return ReadImageValue(image);
    }

    private static IEnumerable<string> ReadKeywords(JsonElement element)
    {
        if (!element.TryGetProperty("keywords", out var keywords))
        {
            yield break;
        }

        if (keywords.ValueKind == JsonValueKind.String)
        {
            foreach (var tag in (keywords.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return tag;
            }
            yield break;
        }

        if (keywords.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in keywords.EnumerateArray().Select(ReadScalarString).Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                yield return tag!;
            }
        }
    }

    private static string? ReadImageValue(JsonElement image)
    {
        if (image.ValueKind == JsonValueKind.String)
        {
            return image.GetString();
        }

        if (image.ValueKind == JsonValueKind.Object)
        {
            return ReadString(image, "url") ?? ReadString(image, "contentUrl");
        }

        return null;
    }

    private static IEnumerable<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            yield return element.GetString() ?? "";
            yield break;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            var value = ReadScalarString(item);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static List<RecipeStepDraft> ReadInstructions(JsonElement element, Uri sourceUri)
    {
        var results = new List<RecipeStepDraft>();
        ReadInstructionsInto(element, results, sourceUri, null);
        return results;
    }

    private static void ReadInstructionsInto(JsonElement element, List<RecipeStepDraft> results, Uri sourceUri, string? section)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                results.Add(new RecipeStepDraft
                {
                    Instruction = StripHtml(element.GetString()) ?? "",
                    Section = section,
                    GroupNumber = 1
                });
                return;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ReadInstructionsInto(item, results, sourceUri, section);
                }
                return;
            case JsonValueKind.Object:
                var typeName = ReadString(element, "@type");
                var nextSection = string.Equals(typeName, "HowToSection", StringComparison.OrdinalIgnoreCase)
                    ? ReadString(element, "name") ?? section
                    : section;

                var text = string.Equals(typeName, "HowToSection", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ReadString(element, "text") ?? ReadString(element, "name");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(new RecipeStepDraft
                    {
                        Instruction = text,
                        Section = nextSection,
                        GroupNumber = 1,
                        ImageUrl = ResolveUrl(ReadImage(element), sourceUri)
                    });
                }

                if (element.TryGetProperty("itemListElement", out var nested))
                {
                    ReadInstructionsInto(nested, results, sourceUri, nextSection);
                }
                return;
        }
    }

    private static RecipeIngredientDraft ParseIngredient(string raw)
    {
        var cleaned = StripHtml(raw)?.Trim() ?? "";
        var match = IngredientRegex.Match(cleaned);
        if (!match.Success)
        {
            return new RecipeIngredientDraft { Name = cleaned, OriginalText = cleaned };
        }

        var quantityText = match.Groups["quantity"].Value.Trim();
        var unit = match.Groups["unit"].Value.Trim();
        var name = match.Groups["name"].Value.Trim();

        return new RecipeIngredientDraft
        {
            Quantity = ParseQuantity(quantityText),
            Unit = string.IsNullOrWhiteSpace(unit) ? null : NormalizeUnit(unit),
            Name = name,
            OriginalText = cleaned
        };
    }

    private static decimal? ParseQuantity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Contains('-') || value.Contains('–'))
        {
            var firstPart = value.Split(['-', '–'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(firstPart) ? null : ParseQuantity(firstPart);
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator)
            && decimal.TryParse(parts[1].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
        {
            return Math.Round(numerator / denominator, 2, MidpointRounding.AwayFromZero);
        }

        return decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static decimal? ParseServings(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = ServingsNumberRegex.Match(value);
        return match.Success ? ParseQuantity(match.Value) : null;
    }

    private static IEnumerable<string> ReadRepeatedItemProp(string html, string itemProp, bool preserveLineBreaks = false)
    {
        var pattern = $"<(?<tag>[a-z0-9]+)[^>]*itemprop=[\"']{Regex.Escape(itemProp)}[\"'][^>]*>(?<content>.*?)</\\k<tag>>";
        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var value = preserveLineBreaks
                ? StripHtmlPreserveLineBreaks(match.Groups["content"].Value)
                : StripHtml(match.Groups["content"].Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static string? ReadItemPropText(string html, string itemProp)
    {
        var metaPattern = $"<meta[^>]*itemprop=[\"']{Regex.Escape(itemProp)}[\"'][^>]*content=[\"'](?<content>.*?)[\"'][^>]*>";
        var metaMatch = Regex.Match(html, metaPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!metaMatch.Success)
        {
            metaPattern = $"<meta[^>]*content=[\"'](?<content>.*?)[\"'][^>]*itemprop=[\"']{Regex.Escape(itemProp)}[\"'][^>]*>";
            metaMatch = Regex.Match(html, metaPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        if (metaMatch.Success)
        {
            return StripHtml(metaMatch.Groups["content"].Value);
        }

        return ReadRepeatedItemProp(html, itemProp).FirstOrDefault();
    }

    private static string? ReadRecipeInfoValue(string html, string label)
    {
        var pattern = $"<tr[^>]*>\\s*<td[^>]*>\\s*{Regex.Escape(label)}\\s*</td>\\s*<td[^>]*>(?<content>.*?)</td>\\s*</tr>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripHtml(match.Groups["content"].Value) : null;
    }

    private static string? ReadRecipeDifficulty(string html)
    {
        var pattern = $"<tr[^>]*>\\s*<td[^>]*>\\s*{Regex.Escape("Schwierigkeitsgrad:")}\\s*</td>\\s*<td[^>]*>(?<content>.*?)</td>\\s*</tr>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        var content = match.Groups["content"].Value;
        var imageText = Regex.Match(content, "(?:alt|title)=[\"'](?<content>[^\"']+)[\"']", RegexOptions.IgnoreCase);
        return imageText.Success ? StripHtml(imageText.Groups["content"].Value) : StripHtml(content);
    }

    private static string? ReadRecipeTableImage(string html)
    {
        var tableMatch = Regex.Match(html, "<table[^>]*class=[\"'][^\"']*rztable[^\"']*[\"'][^>]*>(?<content>.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!tableMatch.Success)
        {
            return null;
        }

        var imageMatch = Regex.Match(tableMatch.Groups["content"].Value, "<img[^>]*src=[\"'](?<src>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return imageMatch.Success ? System.Net.WebUtility.HtmlDecode(imageMatch.Groups["src"].Value) : null;
    }

    private static string? ReadMetaContent(string html, string name)
    {
        var pattern = $"<meta[^>]*name=[\"']{Regex.Escape(name)}[\"'][^>]*content=[\"'](?<content>.*?)[\"'][^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripHtml(match.Groups["content"].Value) : null;
    }

    private static string? ReadOpenGraphContent(string html, string property)
    {
        var pattern = $"<meta[^>]*property=[\"']{Regex.Escape(property)}[\"'][^>]*content=[\"'](?<content>.*?)[\"'][^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripHtml(match.Groups["content"].Value) : null;
    }

    private static string? ReadTitle(string html)
    {
        var match = Regex.Match(html, "<title[^>]*>(?<content>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? CleanPageTitle(StripHtml(match.Groups["content"].Value)) : null;
    }

    private static string? ReadFirstHeading(string html)
    {
        var match = Regex.Match(html, "<h1[^>]*>(?<content>.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripHtml(match.Groups["content"].Value) : null;
    }

    private static string CleanPageTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        return Regex.Replace(title, "\\s+[–-]\\s+.*$", string.Empty).Trim();
    }

    private static string? ReadSectionHtml(string html, string headlineId)
    {
        var pattern = $"<h2[^>]*>.*?id=[\"']{Regex.Escape(headlineId)}[\"'][^>]*>.*?</h2>(?<content>.*?)(?=<h2\\b|$)";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static IEnumerable<(string Text, string? GroupName)> ReadGroupedListItems(string html)
    {
        var groupMatches = Regex.Matches(html, @"<li>\s*(?<group>[^<]+?)\s*<ul>(?<items>.*?)</ul>\s*</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (groupMatches.Count > 0)
        {
            foreach (Match groupMatch in groupMatches)
            {
                var groupName = StripHtml(groupMatch.Groups["group"].Value);
                foreach (var item in ReadFlatListItems(groupMatch.Groups["items"].Value))
                {
                    yield return (item, groupName);
                }
            }

            yield break;
        }

        foreach (var item in ReadFlatListItems(html))
        {
            yield return (item, null);
        }
    }

    private static IEnumerable<(string Text, string? GroupName)> ReadInstructionSections(string html)
    {
        var sectionMatches = Regex.Matches(html, "<h3[^>]*>(?<heading>.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (sectionMatches.Count == 0)
        {
            foreach (var item in ReadFlatListItems(html))
            {
                yield return (item, null);
            }

            yield break;
        }

        for (var i = 0; i < sectionMatches.Count; i++)
        {
            var match = sectionMatches[i];
            var contentStart = match.Index + match.Length;
            var contentEnd = i + 1 < sectionMatches.Count ? sectionMatches[i + 1].Index : html.Length;
            var sectionHtml = html[contentStart..contentEnd];
            var sectionName = StripHtml(match.Groups["heading"].Value)?.Trim().TrimEnd(':');
            foreach (var item in ReadFlatListItems(sectionHtml))
            {
                yield return (item, sectionName);
            }
        }
    }

    private static IEnumerable<string> ReadFlatListItems(string html)
    {
        foreach (Match match in Regex.Matches(html, "<li>(?<content>.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var content = match.Groups["content"].Value;
            var nestedStart = content.LastIndexOf("<li>", StringComparison.OrdinalIgnoreCase);
            if (nestedStart >= 0)
            {
                content = content[(nestedStart + "<li>".Length)..];
            }

            var value = StripHtml(content);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> ReadMediaWikiCategories(string html)
    {
        var catlinksMatch = Regex.Match(html, "<div[^>]*id=[\"']catlinks[\"'][^>]*>(?<content>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!catlinksMatch.Success)
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(catlinksMatch.Groups["content"].Value, "<a[^>]*>(?<content>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var category = StripHtml(match.Groups["content"].Value);
            if (string.IsNullOrWhiteSpace(category)
                || category.Equals("Kategorien", StringComparison.OrdinalIgnoreCase)
                || category.StartsWith("Schwierigkeit", StringComparison.OrdinalIgnoreCase)
                || category.Equals("Rezept des Monats", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return category;
        }
    }

    private static string? ReadDifficulty(string html)
    {
        var match = Regex.Match(html, "Schwierigkeitsgrad.*?<div[^>]*class=[\"'][^\"']*fw-bold[^\"']*[\"'][^>]*>(?<content>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripHtml(match.Groups["content"].Value) : null;
    }

    private static string NormalizeTextLine(string line)
    {
        var normalized = StripHtml(line)?.Trim() ?? string.Empty;
        normalized = LeadingDecorationRegex.Replace(normalized, string.Empty).Trim();
        return normalized;
    }

    private static bool IsSocialNoise(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.StartsWith("was meint ihr")
            || lower == "ja"
            || lower == "nein"
            || lower.StartsWith("ja ") && lower.Contains("nein")
            || lower.Contains(" ja ") && lower.Contains(" nein");
    }

    private static bool TryReadTextSection(string line, out TextRecipeSection section, out decimal? servings)
    {
        servings = ParseServings(line);
        var lower = line.ToLowerInvariant();
        if (lower.Contains("zutaten"))
        {
            section = TextRecipeSection.Ingredients;
            return true;
        }

        if (lower.Contains("zubereitung") || lower.Contains("anleitung") || lower.Contains("zubereiten"))
        {
            section = TextRecipeSection.Steps;
            return true;
        }

        if (lower.Contains("ergebnis") || lower.Contains("tipp") || lower.Contains("hinweis") || lower.Contains("notiz"))
        {
            section = TextRecipeSection.Notes;
            return true;
        }

        section = TextRecipeSection.Title;
        return false;
    }

    private static void ApplyTitleLine(RecipeImportDraft draft, string line)
    {
        var title = Regex.Replace(line, "\\s+", " ").Trim();
        var tagMatches = Regex.Matches(title, "\\((?<tag>[^)]+)\\)");
        foreach (Match match in tagMatches)
        {
            var tag = match.Groups["tag"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                draft.Tags.Add(tag);
            }
        }

        title = Regex.Replace(title, "\\s*\\([^)]*\\)", string.Empty).Trim();
        draft.Name = title;
    }

    private static string TrimStepPrefix(string line) => Regex.Replace(line, @"^\d+[\.)]\s*", string.Empty).Trim();

    private static IEnumerable<string> SplitInstructionText(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = TrimStepPrefix(line);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                yield return cleaned;
            }
        }
    }

    private static int? ParseDurationMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var duration = XmlConvert.ToTimeSpan(value);
            return Math.Max(0, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        }
        catch
        {
            if (ParseHumanDurationMinutes(value) is { } humanDuration)
            {
                return humanDuration;
            }

            var match = ServingsNumberRegex.Match(value);
            return match.Success && int.TryParse(match.Value.Split(',', '.')[0], out var minutes) ? minutes : null;
        }
    }

    private static int? ParseHumanDurationMinutes(string value)
    {
        var total = 0;
        foreach (Match match in Regex.Matches(value, @"(?<number>\d+(?:[\.,]\d+)?)\s*(?<unit>stunden?|std\.?|h|minuten?|min\.?)", RegexOptions.IgnoreCase))
        {
            if (ParseQuantity(match.Groups["number"].Value) is not { } number)
            {
                continue;
            }

            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            total += unit.StartsWith("h", StringComparison.OrdinalIgnoreCase)
                || unit.StartsWith("st", StringComparison.OrdinalIgnoreCase)
                    ? (int)Math.Round(number * 60, MidpointRounding.AwayFromZero)
                    : (int)Math.Round(number, MidpointRounding.AwayFromZero);
        }

        return total > 0 ? total : null;
    }

    private static string NormalizeUnit(string unit) => unit.ToLowerInvariant() switch
    {
        "el" or "esslöffel" or "tbsp" => "EL",
        "tl" or "teelöffel" or "tsp" => "TL",
        "stk" or "stk." or "stück" => "Stück",
        "prise" or "prisen" => "Prise",
        _ => unit
    };

    private static string? ResolveUrl(string? url, Uri sourceUri)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(sourceUri, url, out var resolved) ? resolved.ToString() : url;
    }

    private static string? StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var withoutTags = Regex.Replace(value, "<.*?>", " ");
        return System.Net.WebUtility.HtmlDecode(Regex.Replace(withoutTags, "\\s+", " ").Trim());
    }

    private static string? StripHtmlPreserveLineBreaks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var withLineBreaks = Regex.Replace(value, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        withLineBreaks = Regex.Replace(withLineBreaks, "</p>|</li>|</div>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, "<.*?>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        var lines = decoded.Split('\n')
            .Select(line => Regex.Replace(line, "\\s+", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join("\n", lines);
    }

    private enum TextRecipeSection
    {
        Title,
        Ingredients,
        Steps,
        Notes
    }
}
