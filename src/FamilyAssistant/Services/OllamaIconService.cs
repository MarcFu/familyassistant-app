using System.Text.Json;
using System.Text.RegularExpressions;

namespace FamilyAssistant.Services;

/// <summary>
/// Model size tier used to select prompt complexity and generation parameters.
/// </summary>
public enum ModelTier
{
    /// <summary>≤3B parameters — very short prompt, low num_predict</summary>
    Small = 0,
    /// <summary>4–9B parameters — moderate prompt</summary>
    Medium = 1,
    /// <summary>≥10B parameters — full detailed prompt</summary>
    Large = 2
}

/// <summary>
/// Calls a local Ollama instance to generate an icon (Material Design name or emoji)
/// for a chore based on its name and description.
/// </summary>
public partial class OllamaIconService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaIconService> _logger;

    public OllamaIconService(HttpClient httpClient, ILogger<OllamaIconService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Parses the model name to determine its size tier.
    /// Looks for patterns like ":1b", ":3b", ":7b", ":13b", ":70b" etc.
    /// </summary>
    public static ModelTier DetectModelTier(string modelName)
    {
        // Match patterns like "1b", "1.5b", "3b", "e4b", "7b", "8b", "13b", "70b" in the model name
        // Handles: ":1b", ":e4b", "-7b", ":1.5b", ":q4b" etc.
        var match = ModelSizeRegex().Match(modelName.ToLowerInvariant());
        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var billions))
        {
            return billions switch
            {
                <= 4.0 => ModelTier.Small,
                <= 9.0 => ModelTier.Medium,
                _ => ModelTier.Large
            };
        }

        // If no size found in name, assume medium as safe default
        return ModelTier.Medium;
    }

    [GeneratedRegex(@"[\:\-][a-z]*(\d+\.?\d*)b")]
    private static partial Regex ModelSizeRegex();

    /// <summary>
    /// Ask Ollama to pick a Material Design icon name for the given chore.
    /// </summary>
    public async Task<string?> GetMaterialIconAsync(string name, string? description, string ollamaUrl, string model, CancellationToken ct = default)
    {
        var prompt = $"""
            Du bist ein Icon-Experte. Wähle EIN passendes Material Design Icon für diese Hausarbeit.
            
            Hausarbeit: {name}
            {(description is not null ? $"Beschreibung: {description}" : "")}
            
            Antworte NUR mit dem exakten Icon-Namen (PascalCase), nichts anderes.
            Beispiele gültiger Namen: Kitchen, CleaningServices, LocalLaundryService, Delete, Pets, Grass, ShoppingCart, Bed, Recycling, Restaurant, Iron, Bathroom
            
            Deine Antwort (nur der Icon-Name):
            """;

        return await CallOllamaAsync(prompt, ollamaUrl, model, ct);
    }

    /// <summary>
    /// Ask Ollama to pick a single emoji for the given chore.
    /// </summary>
    public async Task<string?> GetEmojiAsync(string name, string? description, string ollamaUrl, string model, CancellationToken ct = default)
    {
        var prompt = $"""
            Du bist ein Emoji-Experte. Wähle EIN passendes Emoji für diese Hausarbeit.
            
            Hausarbeit: {name}
            {(description is not null ? $"Beschreibung: {description}" : "")}
            
            Antworte NUR mit einem einzigen Emoji-Zeichen, nichts anderes. Kein Text, keine Erklärung.
            
            Dein Emoji:
            """;

        return await CallOllamaAsync(prompt, ollamaUrl, model, ct);
    }

    private async Task<string?> CallOllamaAsync(string prompt, string ollamaUrl, string model, CancellationToken ct)
    {
        try
        {
            var requestBody = new
            {
                model,
                prompt,
                stream = false,
                options = new { temperature = 0.3, num_predict = 30 }
            };

            var url = $"{ollamaUrl.TrimEnd('/')}/api/generate";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("response").GetString()?.Trim();

            if (string.IsNullOrWhiteSpace(result))
                return null;

            // Clean up: take only first word/emoji (Ollama sometimes adds explanation)
            var cleaned = result.Split([' ', '\n', '\r', ',', '.'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return cleaned;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call Ollama at {Url}", ollamaUrl);
            return null;
        }
    }

    /// <summary>
    /// Calls Ollama for free-form text generation. Use this for structured JSON tasks where
    /// callers need the full response instead of the icon-specific single-token cleanup.
    /// </summary>
    public async Task<string?> GenerateTextAsync(
        string prompt,
        string ollamaUrl,
        string model,
        double temperature = 0.1,
        int numPredict = 1200,
        CancellationToken ct = default)
    {
        try
        {
            var requestBody = new
            {
                model,
                prompt,
                stream = false,
                options = new { temperature, num_predict = numPredict }
            };

            var url = $"{ollamaUrl.TrimEnd('/')}/api/generate";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama text generation returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.GetProperty("response").GetString()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call Ollama at {Url} for text generation", ollamaUrl);
            return null;
        }
    }

    /// <summary>
    /// Check if Ollama is reachable and the model is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync(string ollamaUrl, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ollamaUrl.TrimEnd('/')}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lists available models from Ollama.
    /// </summary>
    public async Task<List<string>> GetModelsAsync(string ollamaUrl, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ollamaUrl.TrimEnd('/')}/api/tags", ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var models = json.GetProperty("models").EnumerateArray()
                .Select(m => m.GetProperty("name").GetString()!)
                .ToList();
            return models;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Builds the SVG generation prompt adapted to the model's capability tier.
    /// Public so the UI can display the effective prompt to admins.
    /// </summary>
    public static string BuildSvgPrompt(string description, ModelTier tier)
    {
        return tier switch
        {
            ModelTier.Small => $"""
                Create an SVG icon for: {description}
                Rules: viewBox="0 0 24 24", fill="currentColor", only <path> elements, no text, no explanation.
                Output ONLY the SVG code.
                """,

            ModelTier.Medium => $"""
                Create a simple SVG pictogram in Material Design Icon style.

                Subject: {description}

                Rules:
                - Output only SVG code, no markdown, no explanation.
                - viewBox="0 0 24 24", monochrome fill="currentColor".
                - No background, shadows, gradients, or textures.
                - Prefer <path> elements, keep it simple and geometric.
                - Must be recognizable at 24x24 px.
                - Center the motif, leave ~1px margin.

                Output the SVG now.
                """,

            _ => $"""
                Create a single, clean SVG pictogram in the style of Home Assistant / Material Design Icons.

                User-Description: {description}

                Requirements:
                - Output exclusively as complete SVG code, no markdown, no explanation.
                - ViewBox: 0 0 24 24
                - Monochrome icon, use fill="currentColor".
                - No background, no shadows, no gradients, no textures.
                - No colors other than currentColor.
                - Style: very close to Home Assistant / MDI — flat, simple, geometric, clearly legible.
                - The icon must be clearly recognizable even at 24x24 px.
                - Use as few SVG elements as possible.
                - Prefer one or more <path> elements.
                - No embedded raster graphics, no base64, no text.
                - No unnecessary details.
                - Only use strokes if they appear as filled shapes; otherwise prefer filled silhouettes.
                - The motif must not look cartoonish, illustrative, or photorealistic.
                - It must not look like a style break next to existing Home Assistant icons.

                Composition rules:
                - Optically center the motif in the 24x24 ViewBox.
                - Use the space well but leave approximately 1-2 px margin.
                - Reduce details so that the silhouette remains immediately understandable.
                - If the motif contains multiple objects, combine them into one clear, iconic overall shape.

                Quality check before output:
                - Is the icon recognizable at small sizes?
                - Is it truly monochrome?
                - Does it look like a Home Assistant / MDI-compatible icon?
                - Does the output contain only SVG code?

                Output only the final SVG code now.
                """
        };
    }

    /// <summary>
    /// Returns the num_predict value adapted to model tier.
    /// </summary>
    public static int GetNumPredict(ModelTier tier) => tier switch
    {
        ModelTier.Small => 300,
        ModelTier.Medium => 500,
        _ => 1000
    };

    /// <summary>
    /// Ask Ollama to generate a simple SVG pictogram for the given description.
    /// Returns raw SVG markup or null on failure.
    /// </summary>
    public async Task<string?> GenerateSvgIconAsync(string description, string ollamaUrl, string model, CancellationToken ct = default)
    {
        var tier = DetectModelTier(model);
        var prompt = BuildSvgPrompt(description, tier);
        var numPredict = GetNumPredict(tier);

        _logger.LogInformation("SVG generation: model={Model}, tier={Tier}, num_predict={NumPredict}",
            model, tier, numPredict);

        try
        {
            var requestBody = new
            {
                model,
                prompt,
                stream = false,
                options = new { temperature = 0.7, num_predict = numPredict }
            };

            var url = $"{ollamaUrl.TrimEnd('/')}/api/generate";
            _logger.LogInformation("Requesting SVG generation from {Model} for: {Description}", model, description);

            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama SVG generation returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("response").GetString()?.Trim();

            _logger.LogInformation("Ollama SVG raw response ({Length} chars): {Response}",
                result?.Length ?? 0, result?[..Math.Min(500, result?.Length ?? 0)]);

            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("Ollama returned empty response for SVG generation");
                return null;
            }

            // Extract SVG from response (Ollama might wrap it in markdown code blocks)
            var svgStart = result.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            var svgEnd = result.IndexOf("</svg>", StringComparison.OrdinalIgnoreCase);

            if (svgStart >= 0 && svgEnd > svgStart)
            {
                var svg = result[svgStart..(svgEnd + "</svg>".Length)];
                _logger.LogInformation("Successfully extracted SVG ({Length} chars)", svg.Length);
                return svg;
            }

            // If no full SVG tag, try to wrap path elements
            if (result.Contains("<path", StringComparison.OrdinalIgnoreCase))
            {
                var pathStart = result.IndexOf("<path", StringComparison.OrdinalIgnoreCase);
                var pathEnd = result.LastIndexOf("/>", StringComparison.Ordinal);
                if (pathStart >= 0 && pathEnd > pathStart)
                {
                    var paths = result[pathStart..(pathEnd + 2)];
                    var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">{paths}</svg>""";
                    _logger.LogInformation("Wrapped path elements into SVG ({Length} chars)", svg.Length);
                    return svg;
                }
            }

            _logger.LogWarning("Ollama SVG response did not contain valid SVG: {Response}", result[..Math.Min(500, result.Length)]);
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ct.IsCancellationRequested)
        {
            _logger.LogWarning("SVG generation timed out for model {Model}", model);
            throw; // Let the caller handle timeout specifically
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error calling Ollama at {Url} for SVG generation", ollamaUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate SVG via Ollama at {Url}", ollamaUrl);
            return null;
        }
    }
}
