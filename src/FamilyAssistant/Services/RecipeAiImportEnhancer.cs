using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

public class RecipeAiImportEnhancer(
    AppDbContext db,
    ChoreIconGenerator iconGenerator,
    OllamaIconService ollama,
    RecipeImportTraceService traceService,
    ILogger<RecipeAiImportEnhancer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<RecipeImportDraft> EnhanceAsync(
        RecipeImportDraft draft,
        string sourceText,
        RecipeImportQuality quality,
        string? targetLanguage = null,
        IProgress<RecipeImportProgress>? progress = null,
        RecipeImportTraceContext? trace = null,
        CancellationToken ct = default)
    {
        var ollamaUrl = await iconGenerator.GetOllamaUrlAsync(ct);
        var model = await iconGenerator.GetOllamaModelAsync(ct);
        if (!await ollama.IsAvailableAsync(ollamaUrl, ct))
        {
            logger.LogDebug("Skipping recipe AI enhancement because Ollama is not reachable at {Url}.", ollamaUrl);
            draft.QualityReport = "AI nicht erreichbar; deterministischer Import wurde verwendet.";
            return draft;
        }

        var existingCategories = await db.RecipeCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync(ct);
        var existingTags = await db.RecipeTags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .ToListAsync(ct);

        if (quality == RecipeImportQuality.HighAi)
        {
            return await EnhanceHighEffortAsync(draft, sourceText, existingCategories, existingTags, ollamaUrl, model, targetLanguage, progress, trace, ct);
        }

        progress?.Report(new RecipeImportProgress(95, "AI extrahiert und normalisiert Rezeptdaten", RecipeImportStage.AiExtraction));
        var prompt = BuildPrompt(draft, sourceText, existingCategories, existingTags, quality, targetLanguage);
        var response = await traceService.TraceAsync(
            trace,
            "AiExtraction",
            RecipeImportStage.AiExtraction.ToString(),
            new { quality, prompt = RecipeImportTraceService.TracedText(prompt), draftBefore = ToPromptDraft(draft) },
            () => ollama.GenerateTextAsync(prompt, ollamaUrl, model, temperature: 0.03, numPredict: GetNumPredict(quality), ct),
            output: raw => new { rawResponse = RecipeImportTraceService.TracedText(raw ?? string.Empty) });
        if (string.IsNullOrWhiteSpace(response))
        {
            logger.LogInformation("Recipe AI enhancement returned no result; using deterministic import.");
            draft.QualityReport = "AI hat keine Antwort geliefert; deterministischer Import wurde verwendet.";
            return draft;
        }

        try
        {
            var json = ExtractJson(response);
            var aiDraft = JsonSerializer.Deserialize<AiRecipeDraft>(json, JsonOptions);
            ApplyAiDraft(draft, aiDraft, existingCategories, existingTags);
            NormalizeDraft(draft);
            draft.QualityReport = quality == RecipeImportQuality.QuickAi
                ? "Quick AI: Ein schneller AI-Pass wurde angewendet. Bitte Zutaten und Schritte kurz prüfen."
                : "Medium AI: Ein gründlicher AI-Pass wurde angewendet und das Ergebnis wurde deterministisch validiert.";
            await traceService.TracePointAsync(
                trace,
                "AiExtractionApplied",
                RecipeImportStage.AiExtraction.ToString(),
                new { extractedJson = RecipeImportTraceService.TracedText(json) },
                new { draftAfter = ToPromptDraft(draft) });
            return draft;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Recipe AI enhancement returned invalid JSON.");
            draft.QualityReport = "AI-Antwort war kein gültiges JSON; deterministischer Import wurde verwendet.";
            return draft;
        }
    }

    private async Task<RecipeImportDraft> EnhanceHighEffortAsync(
        RecipeImportDraft draft,
        string sourceText,
        IReadOnlyCollection<string> existingCategories,
        IReadOnlyCollection<string> existingTags,
        string ollamaUrl,
        string model,
        string? targetLanguage,
        IProgress<RecipeImportProgress>? progress,
        RecipeImportTraceContext? trace,
        CancellationToken ct)
    {
        var refinementRounds = 0;
        AiValidationReport? validation = null;

        progress?.Report(new RecipeImportProgress(92, "High AI: Rezept vollständig extrahieren", RecipeImportStage.AiExtraction));
        var extractionPrompt = BuildPrompt(draft, sourceText, existingCategories, existingTags, RecipeImportQuality.HighAi, targetLanguage);
        var extraction = await traceService.TraceAsync(
            trace,
            "AiExtraction",
            RecipeImportStage.AiExtraction.ToString(),
            new { quality = RecipeImportQuality.HighAi, prompt = RecipeImportTraceService.TracedText(extractionPrompt), draftBefore = ToPromptDraft(draft) },
            () => ollama.GenerateTextAsync(
                extractionPrompt,
                ollamaUrl,
                model,
                temperature: 0.02,
                numPredict: GetNumPredict(RecipeImportQuality.HighAi),
                ct),
            output: raw => new { rawResponse = RecipeImportTraceService.TracedText(raw ?? string.Empty) });
        TryApplyAiResponse(draft, extraction, existingCategories, existingTags, "high-effort extraction");
        NormalizeDraft(draft);
        await traceService.TracePointAsync(
            trace,
            "AiExtractionApplied",
            RecipeImportStage.AiExtraction.ToString(),
            new { rawResponse = RecipeImportTraceService.TracedText(extraction ?? string.Empty) },
            new { draftAfter = ToPromptDraft(draft) });

        for (var round = 1; round <= 2; round++)
        {
            progress?.Report(new RecipeImportProgress(94, $"High AI: Ergebnis prüfen (Runde {round})", RecipeImportStage.AiValidation));
            validation = await ValidateWithAiAsync(draft, sourceText, ollamaUrl, model, targetLanguage, trace, round, ct);
            if (validation is null || !validation.HasHardProblems)
            {
                break;
            }

            refinementRounds++;
            progress?.Report(new RecipeImportProgress(96, $"High AI: konkrete Probleme korrigieren (Runde {round})", RecipeImportStage.AiRefinement));
            var refinementPrompt = BuildRefinementPrompt(draft, sourceText, validation, targetLanguage);
            var refinement = await traceService.TraceAsync(
                trace,
                $"AiRefinementRound{round}",
                RecipeImportStage.AiRefinement.ToString(),
                new { round, prompt = RecipeImportTraceService.TracedText(refinementPrompt), validation, draftBefore = ToPromptDraft(draft) },
                () => ollama.GenerateTextAsync(
                    refinementPrompt,
                    ollamaUrl,
                    model,
                    temperature: 0.01,
                    numPredict: GetNumPredict(RecipeImportQuality.HighAi),
                    ct),
                output: raw => new { rawResponse = RecipeImportTraceService.TracedText(raw ?? string.Empty) });
            if (!TryApplyAiResponse(draft, refinement, existingCategories, existingTags, $"high-effort refinement {round}"))
            {
                break;
            }

            NormalizeDraft(draft);
            await traceService.TracePointAsync(
                trace,
                $"AiRefinementRound{round}Applied",
                RecipeImportStage.AiRefinement.ToString(),
                new { rawResponse = RecipeImportTraceService.TracedText(refinement ?? string.Empty) },
                new { draftAfter = ToPromptDraft(draft) });
        }

        progress?.Report(new RecipeImportProgress(98, "High AI: finale Konsistenzprüfung", RecipeImportStage.FinalValidation));
        NormalizeDraft(draft);
        draft.QualityReport = BuildQualityReport(validation, refinementRounds);
        return draft;
    }

    private static string BuildPrompt(
        RecipeImportDraft draft,
        string sourceText,
        IReadOnlyCollection<string> existingCategories,
        IReadOnlyCollection<string> existingTags,
        RecipeImportQuality quality,
        string? targetLanguage)
    {
        var currentDraft = JsonSerializer.Serialize(new
        {
            draft.Name,
            draft.Description,
            draft.Notes,
            draft.BaseServings,
            draft.CategoryName,
            draft.Tags,
            draft.TotalTimeMinutes,
            draft.WorkTimeMinutes,
            draft.CookTimeMinutes,
            draft.RestTimeMinutes,
            draft.Difficulty,
            Ingredients = draft.Ingredients.Select(i => new { i.Quantity, i.Unit, i.Name, i.Note, i.GroupName }),
            Steps = draft.Steps.Select(s => new { s.Instruction, s.InstructionMarkup, s.Section, s.GroupNumber, s.IngredientNames })
        }, JsonOptions);
        var knownTaxonomy = JsonSerializer.Serialize(new
        {
            ExistingCategories = existingCategories,
            ExistingTags = existingTags
        }, JsonOptions);
        var targetLanguageDescription = DescribeTargetLanguage(targetLanguage);
        var markerExample = RecipeStepMarkupHelper.CreateMarker("exact ingredient name");

        return $$"""
            You extract structured recipe data from recipe text or HTML.

            Language rules:
            - Follow these instructions in English.
            - Target recipe language: {{targetLanguageDescription}}.
            - Return user-facing recipe content in the target language: name, description, notes, ingredient names, step instructions, categoryName, tags, difficulty, and groupName.
            - Preserve correct accents, umlauts, and other characters required by the target language.
            - Reuse existing category/tag names verbatim when they fit; create new category/tag names in the target language.

            Rules:
            - Respond with valid JSON only. No Markdown, no explanations.
            - Do not invent ingredients or steps.
            - If preparation is provided as one paragraph, split it into meaningful short steps.
            - Remove ads, navigation, comments, legal disclaimers, and social-media voting text.
            - Preserve quantities, units, and names as exactly as possible.
            - Use null when a field is not reliably visible in the source.
            - Assign one suitable category and several suitable tags when supported by the source.
            - Prefer exact existing categories/tags from the list below.
            - Suggest new categories/tags only when no existing ones fit well.
            - categoryName is the single best category. Additional thematic classifications belong in tags.
            - Use step.section only for real recipe parts such as dough, filling, topping, dressing, or marinade.
            - Device or cooking-method headings such as Airfryer, oven, pan, or Thermomix are normal steps, not sections.
            - Fill ingredientNames for every step with the ingredients used in that step.
            - ingredientNames must exactly match names from ingredients. If unsure, use an empty list.
            - Optionally fill instructionMarkup with the same user-facing instruction text plus ingredient markers.
            - Use marker format exactly as {{markerExample}}.
            - Place markers directly after the word or phrase where the ingredient chip should appear.
            - Only use markers for exact ingredient names from ingredients. If unsure about placement, keep instructionMarkup null and still use ingredientNames.
            {{GetQualityInstruction(quality)}}

            JSON schema:
            {
              "name": "string",
              "description": "string|null",
              "notes": "string|null",
              "baseServings": 4,
              "categoryName": "string|null",
              "tags": ["string"],
              "totalTimeMinutes": 0,
              "workTimeMinutes": 0,
              "cookTimeMinutes": 0,
              "restTimeMinutes": 0,
              "difficulty": "string|null",
              "ingredients": [{"quantity": 1.5, "unit": "g|null", "name": "string", "note": "string|null", "groupName": "string|null"}],
              "steps": [{"instruction": "string", "instructionMarkup": "string|null", "section": "string|null", "groupNumber": 1, "ingredientNames": ["string"]}]
            }

            Current deterministic draft:
            {{currentDraft}}

            Existing categories/tags, prefer reusing them exactly:
            {{knownTaxonomy}}

            Source:
            {{TrimForPrompt(sourceText, GetSourceLimit(quality))}}
            """;
    }

    private async Task<AiValidationReport?> ValidateWithAiAsync(
        RecipeImportDraft draft,
        string sourceText,
        string ollamaUrl,
        string model,
        string? targetLanguage,
        RecipeImportTraceContext? trace,
        int round,
        CancellationToken ct)
    {
        var prompt = BuildValidationPrompt(draft, sourceText, targetLanguage);
        var response = await traceService.TraceAsync(
            trace,
            $"AiValidationRound{round}",
            RecipeImportStage.AiValidation.ToString(),
            new { round, prompt = RecipeImportTraceService.TracedText(prompt), draft = ToPromptDraft(draft) },
            () => ollama.GenerateTextAsync(
                prompt,
                ollamaUrl,
                model,
                temperature: 0.01,
                numPredict: 2200,
                ct),
            output: raw => new { rawResponse = RecipeImportTraceService.TracedText(raw ?? string.Empty) });
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        try
        {
            var validation = JsonSerializer.Deserialize<AiValidationReport>(ExtractJson(response), JsonOptions);
            await traceService.TracePointAsync(
                trace,
                $"AiValidationRound{round}Parsed",
                RecipeImportStage.AiValidation.ToString(),
                new { rawResponse = RecipeImportTraceService.TracedText(response) },
                validation);
            return validation;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Recipe high-effort validation returned invalid JSON.");
            return null;
        }
    }

    private static string BuildValidationPrompt(RecipeImportDraft draft, string sourceText, string? targetLanguage)
    {
        var draftJson = JsonSerializer.Serialize(ToPromptDraft(draft), JsonOptions);
        var targetLanguageDescription = DescribeTargetLanguage(targetLanguage);
        return $$"""
            You are the quality reviewer for a recipe import. Strictly compare the draft against the source.
            Follow these instructions in English. The target recipe language is {{targetLanguageDescription}}.
            Report summaries, problems, corrections, and uncertainties in the target language when practical.

            Respond with valid JSON only:
            {
              "hasHardProblems": true,
              "problemCount": 0,
              "summary": "string",
              "problems": ["string"],
              "corrections": ["string"],
              "uncertainties": ["string"]
            }

            Hard problems include: missing or invented ingredients, wrong quantities/units, missing central steps,
            incorrectly split ingredient groups, obviously wrong step.ingredientNames, and misplaced tips/variants.
            Uncertain times/servings are uncertainties unless the draft clearly contradicts the source.

            Draft:
            {{draftJson}}

            Source:
            {{TrimForPrompt(sourceText, 22000)}}
            """;
    }

    private static string BuildRefinementPrompt(RecipeImportDraft draft, string sourceText, AiValidationReport validation, string? targetLanguage)
    {
        var draftJson = JsonSerializer.Serialize(ToPromptDraft(draft), JsonOptions);
        var validationJson = JsonSerializer.Serialize(validation, JsonOptions);
        var targetLanguageDescription = DescribeTargetLanguage(targetLanguage);
        var markerExample = RecipeStepMarkupHelper.CreateMarker("exact ingredient name");
        return $$"""
            Correct this recipe draft using the validation report and the source.

            Language rules:
            - Follow these instructions in English.
            - Target recipe language: {{targetLanguageDescription}}.
            - Return all user-facing recipe content in the target language.
            - Reuse existing category/tag names verbatim when they fit; create new category/tag names in the target language.

            Rules:
            - Respond only with the complete corrected recipe JSON in the same schema.
            - Correct only concrete problems from the validation report.
            - Do not invent anything. If a value is not reliably visible, use null or leave ingredientNames empty.
            - ingredientNames must exactly match names from ingredients.
            - instructionMarkup may contain {{markerExample}} markers for positioned ingredient chips.
            - Only use markers for exact ingredient names from ingredients. If placement is uncertain, keep instructionMarkup null.

            Recipe JSON schema:
            {
              "name": "string",
              "description": "string|null",
              "notes": "string|null",
              "baseServings": 4,
              "categoryName": "string|null",
              "tags": ["string"],
              "totalTimeMinutes": 0,
              "workTimeMinutes": 0,
              "cookTimeMinutes": 0,
              "restTimeMinutes": 0,
              "difficulty": "string|null",
              "ingredients": [{"quantity": 1.5, "unit": "g|null", "name": "string", "note": "string|null", "groupName": "string|null"}],
              "steps": [{"instruction": "string", "instructionMarkup": "string|null", "section": "string|null", "groupNumber": 1, "ingredientNames": ["string"]}]
            }

            Current draft:
            {{draftJson}}

            Validation report:
            {{validationJson}}

            Source:
            {{TrimForPrompt(sourceText, 22000)}}
            """;
    }

    private bool TryApplyAiResponse(
        RecipeImportDraft draft,
        string? response,
        IReadOnlyCollection<string> existingCategories,
        IReadOnlyCollection<string> existingTags,
        string phase)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            logger.LogInformation("Recipe AI {Phase} returned no result.", phase);
            return false;
        }

        try
        {
            var aiDraft = JsonSerializer.Deserialize<AiRecipeDraft>(ExtractJson(response), JsonOptions);
            ApplyAiDraft(draft, aiDraft, existingCategories, existingTags);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Recipe AI {Phase} returned invalid JSON.", phase);
            return false;
        }
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static int GetSourceLimit(RecipeImportQuality quality) => quality switch
    {
        RecipeImportQuality.QuickAi => 7000,
        RecipeImportQuality.MediumAi => 14000,
        RecipeImportQuality.HighAi => 24000,
        _ => 6000
    };

    private static int GetNumPredict(RecipeImportQuality quality) => quality switch
    {
        RecipeImportQuality.QuickAi => 1600,
        RecipeImportQuality.MediumAi => 2800,
        RecipeImportQuality.HighAi => 5200,
        _ => 1200
    };

    private static string DescribeTargetLanguage(string? targetLanguage)
    {
        var language = NormalizeTargetLanguage(targetLanguage);
        var culture = CultureInfo.GetCultureInfo(language);
        return $"{culture.TwoLetterISOLanguageName} ({culture.EnglishName})";
    }

    private static string NormalizeTargetLanguage(string? targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            return "en";
        }

        var language = targetLanguage.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        return language is "de" or "fr" ? language : "en";
    }

    private static string GetQualityInstruction(RecipeImportQuality quality) => quality switch
    {
        RecipeImportQuality.QuickAi => "Quick mode: Return a solid, compact draft. Prioritize speed over depth.",
        RecipeImportQuality.MediumAi => "Medium mode: Work carefully. Improve groups, steps, tags, and ingredientNames noticeably compared with the deterministic draft.",
        RecipeImportQuality.HighAi => "High mode: The goal is an almost ready-to-accept recipe. Read the source carefully, separate tips/variants/notes cleanly, create useful ingredient groups, and complete step.ingredientNames. Prefer null or empty uncertain fields over invention.",
        _ => string.Empty
    };

    private static object ToPromptDraft(RecipeImportDraft draft) => new
    {
        draft.Name,
        draft.Description,
        draft.Notes,
        draft.BaseServings,
        draft.CategoryName,
        draft.Tags,
        draft.TotalTimeMinutes,
        draft.WorkTimeMinutes,
        draft.CookTimeMinutes,
        draft.RestTimeMinutes,
        draft.Difficulty,
        Ingredients = draft.Ingredients.Select(i => new { i.Quantity, i.Unit, i.Name, i.Note, i.GroupName }),
        Steps = draft.Steps.Select(s => new { s.Instruction, s.InstructionMarkup, s.Section, s.GroupNumber, s.IngredientNames })
    };

    private static string ExtractJson(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("AI response did not contain a JSON object.");
        }

        return response[start..(end + 1)];
    }

    private static RecipeImportDraft ApplyAiDraft(
        RecipeImportDraft draft,
        AiRecipeDraft? aiDraft,
        IReadOnlyCollection<string> existingCategories,
        IReadOnlyCollection<string> existingTags)
    {
        if (aiDraft is null)
        {
            return draft;
        }

        if (!string.IsNullOrWhiteSpace(aiDraft.Name))
        {
            draft.Name = aiDraft.Name.Trim();
        }

        draft.Description = Choose(aiDraft.Description, draft.Description);
        draft.Notes = Choose(aiDraft.Notes, draft.Notes);
        draft.CategoryName = NormalizeWithExisting(Choose(aiDraft.CategoryName, draft.CategoryName), existingCategories);
        draft.Difficulty = Choose(aiDraft.Difficulty, draft.Difficulty);
        draft.BaseServings = aiDraft.BaseServings is > 0 ? aiDraft.BaseServings.Value : draft.BaseServings;
        draft.TotalTimeMinutes = aiDraft.TotalTimeMinutes ?? draft.TotalTimeMinutes;
        draft.WorkTimeMinutes = aiDraft.WorkTimeMinutes ?? draft.WorkTimeMinutes;
        draft.CookTimeMinutes = aiDraft.CookTimeMinutes ?? draft.CookTimeMinutes;
        draft.RestTimeMinutes = aiDraft.RestTimeMinutes ?? draft.RestTimeMinutes;

        if (aiDraft.Tags is { Count: > 0 })
        {
            draft.Tags = NormalizeTagsWithExisting(aiDraft.Tags, existingTags);
        }
        else if (draft.Tags.Count > 0)
        {
            draft.Tags = NormalizeTagsWithExisting(draft.Tags, existingTags);
        }

        if (aiDraft.Ingredients is { Count: > 0 })
        {
            draft.Ingredients = aiDraft.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => new RecipeIngredientDraft
                {
                    Quantity = i.Quantity,
                    Unit = Clean(i.Unit),
                    Name = i.Name!.Trim(),
                    Note = Clean(i.Note),
                    GroupName = Clean(i.GroupName),
                    OriginalText = i.Name!.Trim()
                })
                .ToList();
        }

        if (aiDraft.Steps is { Count: > 0 })
        {
            draft.Steps = aiDraft.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
                .Select(s => new RecipeStepDraft
                {
                    Instruction = s.Instruction!.Trim(),
                    InstructionMarkup = Clean(s.InstructionMarkup),
                    Section = Clean(s.Section),
                    GroupNumber = s.GroupNumber is > 0 ? s.GroupNumber.Value : 1,
                    IngredientNames = s.IngredientNames?
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? []
                })
                .ToList();
        }

        return draft;
    }

    private static string? Choose(string? preferred, string? fallback) => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeWithExisting(string? value, IReadOnlyCollection<string> existingValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return existingValues.FirstOrDefault(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)) ?? trimmed;
    }

    private static List<string> NormalizeTagsWithExisting(IEnumerable<string> tags, IReadOnlyCollection<string> existingTags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => NormalizeWithExisting(tag, existingTags)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void NormalizeDraft(RecipeImportDraft draft)
    {
        draft.Name = draft.Name.Trim();
        draft.Description = Clean(draft.Description);
        draft.Notes = Clean(draft.Notes);
        draft.CategoryName = Clean(draft.CategoryName);
        draft.Difficulty = Clean(draft.Difficulty);
        draft.BaseServings = draft.BaseServings <= 0 ? 4 : draft.BaseServings;
        draft.Tags = draft.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        draft.Ingredients = draft.Ingredients
            .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient.Name))
            .Select(ingredient =>
            {
                ingredient.Name = ingredient.Name.Trim();
                ingredient.Unit = Clean(ingredient.Unit);
                ingredient.Note = Clean(ingredient.Note);
                ingredient.GroupName = Clean(ingredient.GroupName);
                ingredient.OriginalText = Clean(ingredient.OriginalText) ?? ingredient.Name;
                return ingredient;
            })
            .ToList();

        var ingredientsByNormalizedName = draft.Ingredients
            .GroupBy(ingredient => NormalizeName(ingredient.Name))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

        draft.Steps = draft.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.Instruction))
            .Select(step =>
            {
                step.Instruction = step.Instruction.Trim();
                step.InstructionMarkup = Clean(step.InstructionMarkup);
                step.Section = Clean(step.Section);
                step.GroupNumber = step.GroupNumber <= 0 ? 1 : step.GroupNumber;
                step.IngredientNames = step.IngredientNames
                    .Select(name => NormalizeName(name))
                    .Where(name => ingredientsByNormalizedName.ContainsKey(name))
                    .Select(name => ingredientsByNormalizedName[name])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return step;
            })
            .ToList();
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit));
    }

    private static string BuildQualityReport(AiValidationReport? validation, int refinementRounds)
    {
        var lines = new List<string>
        {
            $"High AI: Extraction, Validation und {refinementRounds} Refinement-Runde(n) abgeschlossen."
        };

        if (validation is null)
        {
            lines.Add("AI-Validierung lieferte keinen auswertbaren Bericht; deterministische Konsistenzprüfung wurde angewendet.");
            return string.Join("\n", lines);
        }

        lines.Add(validation.HasHardProblems
            ? "Validierung meldete nach den Refinements noch prüfbedürftige Punkte."
            : "Validierung meldete keine harten Probleme.");
        if (!string.IsNullOrWhiteSpace(validation.Summary))
        {
            lines.Add(validation.Summary.Trim());
        }

        foreach (var uncertainty in validation.Uncertainties?.Where(u => !string.IsNullOrWhiteSpace(u)).Take(5) ?? [])
        {
            lines.Add($"Unsicher: {uncertainty.Trim()}");
        }

        foreach (var problem in validation.Problems?.Where(p => !string.IsNullOrWhiteSpace(p)).Take(5) ?? [])
        {
            lines.Add($"Prüfen: {problem.Trim()}");
        }

        return string.Join("\n", lines);
    }

    private sealed class AiValidationReport
    {
        public bool HasHardProblems { get; set; }
        public int ProblemCount { get; set; }
        public string? Summary { get; set; }
        public List<string>? Problems { get; set; }
        public List<string>? Corrections { get; set; }
        public List<string>? Uncertainties { get; set; }
    }

    private sealed class AiRecipeDraft
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? BaseServings { get; set; }
        public string? CategoryName { get; set; }
        public List<string>? Tags { get; set; }
        public int? TotalTimeMinutes { get; set; }
        public int? WorkTimeMinutes { get; set; }
        public int? CookTimeMinutes { get; set; }
        public int? RestTimeMinutes { get; set; }
        public string? Difficulty { get; set; }
        public List<AiIngredientDraft>? Ingredients { get; set; }
        public List<AiStepDraft>? Steps { get; set; }
    }

    private sealed class AiIngredientDraft
    {
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Name { get; set; }
        public string? Note { get; set; }
        public string? GroupName { get; set; }
    }

    private sealed class AiStepDraft
    {
        public string? Instruction { get; set; }
        public string? InstructionMarkup { get; set; }
        public string? Section { get; set; }
        public int? GroupNumber { get; set; }
        public List<string>? IngredientNames { get; set; }
    }
}
