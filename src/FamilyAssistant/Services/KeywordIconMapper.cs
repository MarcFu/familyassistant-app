namespace FamilyAssistant.Services;

/// <summary>
/// Maps German household chore keywords to Material Design icon names.
/// Fast, offline, no external dependency.
/// </summary>
public static class KeywordIconMapper
{
    // Material Design icon names (matching MudBlazor Icons.Material.Filled.*)
    private static readonly (string[] Keywords, string Icon)[] Mappings =
    [
        // Kitchen / Cooking
        (["küche", "kochen", "abwasch", "geschirr", "spülen", "abtrocknen"], "Kitchen"),
        (["spülmaschine", "geschirrspüler"], "Dishwasher"),
        (["essen", "mahlzeit", "frühstück", "mittag", "abendessen", "tisch decken"], "Restaurant"),
        (["backen", "kuchen"], "BakeryDining"),
        (["einkauf", "einkaufen", "supermarkt", "laden"], "ShoppingCart"),
        
        // Cleaning
        (["staubsaugen", "saugen", "staubsauger"], "Vacuum"),
        (["saugroboter", "roboter sauger", "robotersauger"], "RobotVacuum"),
        (["fegen", "kehren", "besen"], "Broom"),
        (["wischen", "moppen", "boden"], "CleaningServices"),
        (["sprühflasche", "reiniger", "putzmittel"], "SprayBottle"),
        (["putzen", "reinigen", "sauber", "bad putzen", "badezimmer"], "CleaningServices"),
        (["staub", "abstauben", "staubwischen"], "Air"),
        (["fenster", "fenster putzen"], "Window"),
        (["müll", "mülleimer", "abfall", "tonne", "biotonne", "gelber sack"], "Delete"),
        (["aufräumen", "ordnung", "sortieren", "zimmer aufräumen"], "Inventory2"),
        
        // Laundry
        (["wäsche", "waschen"], "LocalLaundryService"),
        (["waschmaschine"], "WashingMachine"),
        (["trockner"], "TumbleDryer"),
        (["bügeln", "bügeleisen"], "Iron"),
        (["zusammenlegen", "falten", "kleidung"], "Checkroom"),
        
        // Garden / Outdoor
        (["garten", "rasen", "mähen", "rasenmähen"], "Grass"),
        (["blumen", "gießen", "pflanzen", "bewässern"], "LocalFlorist"),
        (["laub", "harken", "unkraut"], "Park"),
        (["schnee", "schneeschaufeln"], "AcUnit"),
        (["auto", "autowaschen", "garage"], "DirectionsCar"),
        
        // Pets
        (["katze", "katzenklo", "katzenfutter"], "Pets"),
        (["hund", "gassi", "hundefutter", "füttern"], "Pets"),
        (["tier", "haustier", "aquarium", "fisch"], "Pets"),
        
        // Kids / School
        (["hausaufgaben", "schule", "lernen", "üben"], "School"),
        (["lesen", "buch", "vorlesen"], "MenuBook"),
        (["instrument", "klavier", "gitarre", "musik", "üben"], "MusicNote"),
        
        // Home maintenance
        (["reparatur", "reparieren", "werkzeug", "schrauben"], "Build"),
        (["glühbirne", "lampe", "licht"], "LightbulbOutline"),
        (["batterie", "batterien"], "BatteryFull"),
        (["post", "briefkasten", "brief"], "Mail"),
        
        // Bathroom / Hygiene
        (["bad", "dusche", "badewanne", "toilette", "wc", "klo"], "Bathroom"),
        (["zähne", "zähneputzen"], "Face"),
        
        // Bed / Bedroom
        (["bett", "betten", "bettdecke", "bettwäsche", "schlafzimmer"], "Bed"),
        
        // Recycling
        (["recycling", "altpapier", "altglas", "pfand"], "Recycling"),
        
        // Generic
        (["helfen", "hilfe", "unterstützung"], "VolunteerActivism"),
        (["termin", "arzt", "zahnarzt"], "Event"),
    ];

    /// <summary>
    /// Finds the best matching Material Design icon name for the given chore name/description.
    /// Returns null if no match found.
    /// </summary>
    public static string? GetIcon(string name, string? description = null)
    {
        var options = GetIconOptions(name, description);
        return options.FirstOrDefault();
    }

    /// <summary>
    /// Returns multiple matching icons, sorted by relevance (best match first).
    /// Always includes some general-purpose icons as fallback options.
    /// </summary>
    public static List<string> GetIconOptions(string name, string? description = null, int maxResults = 6)
    {
        var searchText = $"{name} {description}".ToLowerInvariant();
        var matches = new List<(string Icon, int Score)>();

        foreach (var (keywords, icon) in Mappings)
        {
            var bestScore = 0;
            foreach (var keyword in keywords)
            {
                if (searchText.Contains(keyword) && keyword.Length > bestScore)
                {
                    bestScore = keyword.Length;
                }
            }
            if (bestScore > 0)
            {
                matches.Add((icon, bestScore));
            }
        }

        var result = matches
            .OrderByDescending(m => m.Score)
            .Select(m => m.Icon)
            .Distinct()
            .Take(maxResults)
            .ToList();

        // Pad with general fallback icons if we have few matches
        var fallbacks = new[] { "Task", "CleaningServices", "Home", "Star", "CheckCircle", "Handyman" };
        foreach (var fb in fallbacks)
        {
            if (result.Count >= maxResults) break;
            if (!result.Contains(fb))
                result.Add(fb);
        }

        return result.Take(maxResults).ToList();
    }

    /// <summary>
    /// Gets the SVG path for rendering. Includes MudBlazor icons and curated Home Assistant/MDI additions.
    /// </summary>
    public static string? GetMudIcon(string iconName)
    {
        // We store just the icon name and resolve it at render time.
        return IconRegistry.GetSvg(iconName);
    }
}
