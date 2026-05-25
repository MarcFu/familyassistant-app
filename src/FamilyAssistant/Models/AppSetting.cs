namespace FamilyAssistant.Models;

/// <summary>
/// Key-value store for app-wide settings (persisted in DB).
/// </summary>
public class AppSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
}

public enum IconMode
{
    /// <summary>Keyword-based mapping to Material Design icons (offline)</summary>
    Keyword,
    /// <summary>Local Ollama picks a Material Design icon</summary>
    OllamaMaterial,
    /// <summary>Local Ollama picks an emoji</summary>
    OllamaEmoji
}
