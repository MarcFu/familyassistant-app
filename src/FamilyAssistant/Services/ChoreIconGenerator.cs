using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

/// <summary>
/// Generates icons for chores using the configured strategy (Keyword, Ollama Material, Ollama Emoji).
/// </summary>
public class ChoreIconGenerator
{
    private readonly AppDbContext _db;
    private readonly OllamaIconService _ollama;
    private readonly ILogger<ChoreIconGenerator> _logger;

    // Settings keys
    public const string SettingIconMode = "IconMode";
    public const string SettingOllamaUrl = "OllamaUrl";
    public const string SettingOllamaModel = "OllamaModel";

    // Defaults
    public const string DefaultOllamaUrl = "http://localhost:11434";
    public const string DefaultOllamaModel = "llama3.2:1b";

    public ChoreIconGenerator(AppDbContext db, OllamaIconService ollama, ILogger<ChoreIconGenerator> logger)
    {
        _db = db;
        _ollama = ollama;
        _logger = logger;
    }

    /// <summary>
    /// Generate an icon for the given chore name/description using the configured mode.
    /// Returns the icon string (Material icon name or emoji), or null if generation failed.
    /// </summary>
    public async Task<string?> GenerateIconAsync(string name, string? description, CancellationToken ct = default)
    {
        var mode = await GetSettingAsync(SettingIconMode, ct);
        var iconMode = Enum.TryParse<IconMode>(mode, out var parsed) ? parsed : IconMode.Keyword;

        return iconMode switch
        {
            IconMode.Keyword => KeywordIconMapper.GetIcon(name, description),
            IconMode.OllamaMaterial => await GenerateOllamaMaterialAsync(name, description, ct),
            IconMode.OllamaEmoji => await GenerateOllamaEmojiAsync(name, description, ct),
            _ => KeywordIconMapper.GetIcon(name, description)
        };
    }

    /// <summary>
    /// Get the current icon mode from settings.
    /// </summary>
    public async Task<IconMode> GetIconModeAsync(CancellationToken ct = default)
    {
        var mode = await GetSettingAsync(SettingIconMode, ct);
        return Enum.TryParse<IconMode>(mode, out var parsed) ? parsed : IconMode.Keyword;
    }

    /// <summary>
    /// Get the configured Ollama URL.
    /// </summary>
    public async Task<string> GetOllamaUrlAsync(CancellationToken ct = default)
    {
        return await GetSettingAsync(SettingOllamaUrl, ct) ?? DefaultOllamaUrl;
    }

    /// <summary>
    /// Get the configured Ollama model.
    /// </summary>
    public async Task<string> GetOllamaModelAsync(CancellationToken ct = default)
    {
        return await GetSettingAsync(SettingOllamaModel, ct) ?? DefaultOllamaModel;
    }

    /// <summary>
    /// Save a setting value.
    /// </summary>
    public async Task SetSettingAsync(string key, string? value, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings.FindAsync([key], ct);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string?> GenerateOllamaMaterialAsync(string name, string? description, CancellationToken ct)
    {
        var url = await GetOllamaUrlAsync(ct);
        var model = await GetOllamaModelAsync(ct);

        var icon = await _ollama.GetMaterialIconAsync(name, description, url, model, ct);

        // Validate it's a real icon
        if (icon is not null && IconRegistry.Exists(icon))
            return icon;

        // Fallback to keyword if Ollama gave invalid icon name
        _logger.LogDebug("Ollama returned invalid icon '{Icon}', falling back to keyword", icon);
        return KeywordIconMapper.GetIcon(name, description);
    }

    private async Task<string?> GenerateOllamaEmojiAsync(string name, string? description, CancellationToken ct)
    {
        var url = await GetOllamaUrlAsync(ct);
        var model = await GetOllamaModelAsync(ct);

        var emoji = await _ollama.GetEmojiAsync(name, description, url, model, ct);

        // Basic validation: should be 1-2 characters (emoji can be multi-byte)
        if (emoji is not null && emoji.Length <= 4 && !emoji.All(char.IsAsciiLetter))
            return emoji;

        _logger.LogDebug("Ollama returned invalid emoji '{Emoji}', falling back to keyword", emoji);
        return KeywordIconMapper.GetIcon(name, description);
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
    {
        var setting = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value;
    }
}
