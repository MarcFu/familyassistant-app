namespace FamilyAssist.Configuration;

public class HomeAssistantOptions
{
    public const string SectionName = "HomeAssistant";

    /// <summary>
    /// Base URL of the Home Assistant instance.
    /// Development: http://homeassistant:8123
    /// Add-on: http://supervisor/core
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Long-Lived Access Token (Development) or Supervisor Token (Add-on).
    /// </summary>
    public required string Token { get; set; }
}
