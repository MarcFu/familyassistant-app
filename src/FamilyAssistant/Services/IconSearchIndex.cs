using System.Text;
using System.Text.RegularExpressions;

namespace FamilyAssistant.Services;

/// <summary>
/// Bidirectional search index for icons: supports English icon names (CamelCase split),
/// German keywords, and fuzzy/substring matching.
/// </summary>
public static partial class IconSearchIndex
{
    private static readonly Lazy<Dictionary<string, List<string>>> _deToIcons = new(BuildGermanIndex);
    private static readonly Lazy<Dictionary<string, List<string>>> _enToIcons = new(BuildEnglishIndex);

    /// <summary>
    /// Search for icons matching a query string. Searches both DE keywords and EN names.
    /// Returns icon names sorted by relevance (exact match first, then prefix, then contains).
    /// </summary>
    public static List<string> Search(string query, int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var q = query.Trim().ToLowerInvariant();
        var results = new Dictionary<string, int>(); // icon name → score

        // Search German keywords
        foreach (var (keyword, icons) in _deToIcons.Value)
        {
            var score = GetMatchScore(keyword, q);
            if (score > 0)
            {
                foreach (var icon in icons)
                {
                    results.TryGetValue(icon, out var existing);
                    results[icon] = Math.Max(existing, score);
                }
            }
        }

        // Search English (split CamelCase names)
        foreach (var (word, icons) in _enToIcons.Value)
        {
            var score = GetMatchScore(word, q);
            if (score > 0)
            {
                foreach (var icon in icons)
                {
                    results.TryGetValue(icon, out var existing);
                    results[icon] = Math.Max(existing, score);
                }
            }
        }

        // Also direct name match (case-insensitive)
        foreach (var name in IconRegistry.All.Keys)
        {
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.TryGetValue(name, out var existing);
                var directScore = name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
                results[name] = Math.Max(existing, directScore);
            }
        }

        return results
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(maxResults)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static int GetMatchScore(string indexed, string query)
    {
        // Try exact match first
        var score = GetRawScore(indexed, query);
        if (score > 0) return score;

        // Try normalized (umlauts removed) match
        var normalizedIndexed = NormalizeUmlauts(indexed);
        var normalizedQuery = NormalizeUmlauts(query);

        if (normalizedIndexed != indexed || normalizedQuery != query)
        {
            var normalizedScore = GetRawScore(normalizedIndexed, normalizedQuery);
            // Slightly lower score for normalized matches
            if (normalizedScore > 0) return normalizedScore - 5;
        }

        return 0;
    }

    private static int GetRawScore(string indexed, string query)
    {
        if (indexed == query) return 100;           // exact
        if (indexed.StartsWith(query)) return 80;   // prefix
        if (indexed.Contains(query)) return 40;     // substring
        return 0;
    }

    /// <summary>
    /// Normalizes German umlauts and ß for tolerant search.
    /// ä→a, ö→o, ü→u, ß→ss
    /// </summary>
    public static string NormalizeUmlauts(string input)
    {
        return input
            .Replace("ä", "a").Replace("Ä", "A")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ü", "u").Replace("Ü", "U")
            .Replace("ß", "ss");
    }

    /// <summary>
    /// Splits a PascalCase name into lowercase words.
    /// "CleaningServices" → ["cleaning", "services"]
    /// </summary>
    public static List<string> SplitCamelCase(string name)
    {
        var words = CamelCaseRegex().Split(name)
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w.ToLowerInvariant())
            .ToList();
        return words;
    }

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex CamelCaseRegex();

    private static Dictionary<string, List<string>> BuildEnglishIndex()
    {
        var index = new Dictionary<string, List<string>>(4000);
        foreach (var name in IconRegistry.All.Keys)
        {
            var words = SplitCamelCase(name);
            foreach (var word in words)
            {
                if (word.Length < 2) continue;
                if (!index.TryGetValue(word, out var list))
                {
                    list = [];
                    index[word] = list;
                }
                list.Add(name);
            }
        }
        return index;
    }

    private static Dictionary<string, List<string>> BuildGermanIndex()
    {
        var index = new Dictionary<string, List<string>>(600);

        // Comprehensive German keyword → icon names mapping
        var mappings = new Dictionary<string, string[]>
        {
            // Haushalt
            ["putzen"] = ["CleaningServices", "Soap", "CleanHands"],
            ["reinigen"] = ["CleaningServices", "Soap", "WashOutlined", "CleanHands"],
            ["saugen"] = ["CleaningServices"],
            ["staubsaugen"] = ["CleaningServices"],
            ["wischen"] = ["CleaningServices"],
            ["fegen"] = ["CleaningServices"],
            ["kehren"] = ["CleaningServices"],
            ["staub"] = ["CleaningServices", "Air"],
            ["aufräumen"] = ["CleaningServices", "Inventory2", "Shelves"],
            ["ordnung"] = ["Inventory2", "Shelves", "Sort"],
            ["sortieren"] = ["Sort", "Inventory2"],
            ["waschen"] = ["LocalLaundryService", "WashOutlined", "Soap"],
            ["wäsche"] = ["LocalLaundryService", "DryCleaningOutlet", "Checkroom"],
            ["trockner"] = ["LocalLaundryService", "DryCleaningOutlet"],
            ["bügeln"] = ["Iron"],
            ["bett"] = ["Bed", "SingleBed", "KingBed"],
            ["betten"] = ["Bed", "SingleBed", "KingBed"],
            ["schlafzimmer"] = ["Bed", "KingBed", "Bedroom"],
            ["bad"] = ["Bathtub", "Shower", "Bathroom"],
            ["badezimmer"] = ["Bathtub", "Shower", "Bathroom"],
            ["dusche"] = ["Shower"],
            ["toilette"] = ["Bathroom", "Wc"],
            ["klo"] = ["Bathroom", "Wc"],
            ["fenster"] = ["Window", "SensorWindow"],
            ["tür"] = ["DoorFront", "DoorBack", "SensorDoor", "DoorSliding"],
            ["vorhang"] = ["Curtains", "CurtainsClosed"],
            ["gardine"] = ["Curtains", "CurtainsClosed"],
            ["boden"] = ["CleaningServices", "Foundation"],
            ["treppe"] = ["Stairs"],
            ["garage"] = ["Garage"],
            ["keller"] = ["Warehouse", "Foundation"],
            ["dach"] = ["Roofing"],
            ["licht"] = ["Light", "Lightbulb", "WbIncandescent", "Lamp"],
            ["lampe"] = ["Lamp", "Light", "Lightbulb"],
            ["strom"] = ["ElectricalServices", "Power", "Outlet", "Bolt"],
            ["möbel"] = ["Chair", "Weekend", "TableBar"],
            ["tisch"] = ["TableBar", "TableRestaurant"],
            ["stuhl"] = ["Chair", "EventSeat"],
            ["sofa"] = ["Weekend"],
            ["couch"] = ["Weekend"],
            ["regal"] = ["Shelves"],
            ["schrank"] = ["Shelves", "Inventory2", "Checkroom"],

            // Küche
            ["küche"] = ["Kitchen", "Countertops"],
            ["kochen"] = ["Restaurant", "SoupKitchen", "LocalDining", "Oven"],
            ["essen"] = ["Restaurant", "LocalDining", "DinnerDining", "LunchDining"],
            ["geschirr"] = ["Kitchen", "LocalDining", "DinnerDining"],
            ["spülen"] = ["Kitchen", "WaterDrop", "LocalDining"],
            ["geschirrspüler"] = ["Kitchen", "DinnerDining"],
            ["abwaschen"] = ["Kitchen", "WaterDrop"],
            ["abtrocknen"] = ["Kitchen"],
            ["backen"] = ["BakeryDining", "Cake", "Cookie"],
            ["kuchen"] = ["Cake", "BakeryDining"],
            ["kaffee"] = ["Coffee", "CoffeeMaker", "LocalCafe", "FreeBreakfast"],
            ["tee"] = ["EmojiFoodBeverage", "FreeBreakfast"],
            ["frühstück"] = ["FreeBreakfast", "BrunchDining", "Egg"],
            ["mittag"] = ["LunchDining", "Restaurant"],
            ["abend"] = ["DinnerDining", "Restaurant"],
            ["mikrowelle"] = ["Microwave"],
            ["ofen"] = ["Oven"],
            ["herd"] = ["Oven", "LocalFireDepartment"],
            ["kühlschrank"] = ["Kitchen", "AcUnit"],
            ["einkaufen"] = ["ShoppingCart", "ShoppingBasket", "LocalGroceryStore"],
            ["einkauf"] = ["ShoppingCart", "ShoppingBasket", "Store"],
            ["supermarkt"] = ["LocalGroceryStore", "Store"],

            // Garten
            ["garten"] = ["Yard", "Grass", "Park"],
            ["rasen"] = ["Grass", "Yard"],
            ["mähen"] = ["Grass", "Yard", "ContentCut"],
            ["rasenmähen"] = ["Grass", "Yard"],
            ["blumen"] = ["LocalFlorist", "FilterVintage", "Spa"],
            ["pflanzen"] = ["LocalFlorist", "Spa", "EmojiNature", "Park"],
            ["gießen"] = ["WaterDrop", "Opacity", "Spa"],
            ["hecke"] = ["Fence", "Park", "ContentCut"],
            ["laub"] = ["EnergySavingsLeaf", "Eco", "Yard"],
            ["kompost"] = ["Compost", "Eco"],
            ["unkraut"] = ["Grass", "Yard"],
            ["pool"] = ["Pool", "HotTub"],
            ["grillen"] = ["OutdoorGrill"],
            ["terrasse"] = ["Deck", "Balcony"],
            ["balkon"] = ["Balcony"],

            // Tiere
            ["katze"] = ["Pets", "CrueltyFree"],
            ["kater"] = ["Pets", "CrueltyFree"],
            ["hund"] = ["Pets"],
            ["füttern"] = ["Pets", "SetMeal", "RiceBowl"],
            ["futter"] = ["Pets", "SetMeal"],
            ["katzenklo"] = ["Pets", "Delete"],
            ["streu"] = ["Pets"],
            ["tierarzt"] = ["Pets", "LocalHospital"],
            ["gassi"] = ["Pets", "DirectionsWalk"],
            ["aquarium"] = ["Pool", "Water"],
            ["vogel"] = ["EmojiNature"],
            ["hamster"] = ["Pets", "CrueltyFree"],

            // Müll
            ["müll"] = ["Delete", "DeleteForever", "DeleteSweep"],
            ["mülleimer"] = ["Delete", "DeleteForever"],
            ["tonne"] = ["Delete", "DeleteForever", "Recycling"],
            ["mülltonne"] = ["Delete", "DeleteForever"],
            ["gelbe"] = ["Delete", "Recycling"],
            ["biotonne"] = ["Compost", "Delete", "Eco"],
            ["restmüll"] = ["Delete", "DeleteForever"],
            ["papier"] = ["Delete", "Recycling", "Description"],
            ["altpapier"] = ["Delete", "Recycling"],
            ["recycling"] = ["Recycling"],
            ["wertstoff"] = ["Recycling"],
            ["entsorgen"] = ["Delete", "DeleteSweep"],
            ["altglas"] = ["Delete", "LocalBar"],

            // Kinder & Schule
            ["hausaufgaben"] = ["MenuBook", "School", "Create"],
            ["schule"] = ["School", "MenuBook", "Backpack"],
            ["lernen"] = ["School", "MenuBook", "AutoStories"],
            ["lesen"] = ["AutoStories", "MenuBook", "Book"],
            ["üben"] = ["School", "Piano", "FitnessCenter"],
            ["klavier"] = ["Piano", "MusicNote"],
            ["musik"] = ["MusicNote", "Piano", "Headphones"],
            ["instrument"] = ["Piano", "MusicNote"],
            ["sport"] = ["FitnessCenter", "SportsScore", "DirectionsRun"],
            ["spielen"] = ["SportsEsports", "SmartToy", "Toys"],
            ["kinder"] = ["ChildCare", "ChildFriendly", "FamilyRestroom"],
            ["baby"] = ["ChildCare", "ChildFriendly"],
            ["ranzen"] = ["Backpack", "School"],

            // Werkzeug
            ["reparieren"] = ["Build", "Handyman", "Construction"],
            ["reparatur"] = ["Build", "Handyman"],
            ["werkzeug"] = ["Build", "Handyman", "Construction"],
            ["bohren"] = ["Build", "Hardware"],
            ["schrauben"] = ["Build", "Settings"],
            ["streichen"] = ["FormatPaint", "ImagesearchRoller", "Brush"],
            ["malen"] = ["Brush", "ColorLens", "Palette", "FormatPaint"],
            ["farbe"] = ["FormatPaint", "ColorLens", "Palette"],

            // Auto & Transport
            ["auto"] = ["DirectionsCar", "CarRepair", "LocalCarWash"],
            ["autowaschen"] = ["LocalCarWash"],
            ["tanken"] = ["LocalGasStation", "EvStation"],
            ["fahrrad"] = ["DirectionsBike", "PedalBike"],
            ["rad"] = ["DirectionsBike", "PedalBike"],
            ["roller"] = ["ElectricScooter", "TwoWheeler"],

            // Gesundheit
            ["arzt"] = ["MedicalServices", "LocalHospital", "LocalPharmacy"],
            ["medikamente"] = ["Medication", "LocalPharmacy"],
            ["sport"] = ["FitnessCenter", "DirectionsRun"],
            ["fitness"] = ["FitnessCenter", "SportsGymnastics"],
            ["yoga"] = ["SelfImprovement"],
            ["zahnarzt"] = ["MedicalServices"],
            ["apotheke"] = ["LocalPharmacy"],

            // Zeit & Organisation
            ["termin"] = ["Event", "CalendarToday"],
            ["kalender"] = ["CalendarToday", "CalendarMonth", "DateRange"],
            ["wecker"] = ["Alarm", "AlarmOn"],
            ["timer"] = ["Timer", "HourglassEmpty"],
            ["erinnerung"] = ["NotificationsActive", "Alarm"],
            ["liste"] = ["Checklist", "ChecklistRtl", "Assignment"],
            ["planen"] = ["CalendarMonth", "Event", "Schedule"],
        };

        foreach (var (keyword, icons) in mappings)
        {
            index[keyword] = icons.ToList();
        }

        return index;
    }
}
