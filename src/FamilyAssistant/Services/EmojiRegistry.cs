namespace FamilyAssistant.Services;

/// <summary>
/// Curated set of ~150 household-relevant emojis with categories and German keywords.
/// </summary>
public static class EmojiRegistry
{
    public static readonly IReadOnlyList<EmojiCategory> Categories =
    [
        new("Haushalt", [
            new("🧹", "Besen", ["putzen", "fegen", "kehren"]),
            new("🧽", "Schwamm", ["putzen", "wischen", "reinigen"]),
            new("🪣", "Eimer", ["putzen", "wischen", "wasser"]),
            new("🧺", "Wäschekorb", ["wäsche", "waschen"]),
            new("🧴", "Seife", ["reinigen", "waschen", "bad"]),
            new("🧼", "Seifenstück", ["waschen", "reinigen", "hände"]),
            new("🪒", "Rasierer", ["bad", "pflege"]),
            new("🧻", "Toilettenpapier", ["bad", "toilette", "klo"]),
            new("🚿", "Dusche", ["bad", "dusche", "waschen"]),
            new("🛁", "Badewanne", ["bad", "baden"]),
            new("🚽", "Toilette", ["bad", "klo", "toilette"]),
            new("🛏️", "Bett", ["bett", "schlafen", "schlafzimmer"]),
            new("🪑", "Stuhl", ["möbel", "stuhl", "sitzen"]),
            new("🛋️", "Sofa", ["sofa", "couch", "wohnzimmer"]),
            new("🪟", "Fenster", ["fenster", "putzen", "lüften"]),
            new("🚪", "Tür", ["tür", "eingang"]),
            new("🪞", "Spiegel", ["spiegel", "bad", "putzen"]),
            new("🧲", "Magnet", ["kühlschrank", "magnet"]),
            new("🔑", "Schlüssel", ["schlüssel", "tür", "haus"]),
            new("💡", "Glühbirne", ["licht", "lampe", "strom"]),
            new("🔌", "Stecker", ["strom", "stecker", "laden"]),
            new("🪫", "Batterie leer", ["batterie", "laden", "wechseln"]),
            new("🧯", "Feuerlöscher", ["feuer", "sicherheit"]),
        ]),

        new("Küche", [
            new("🍽️", "Gedeck", ["essen", "tisch", "geschirr"]),
            new("🍳", "Pfanne", ["kochen", "braten", "frühstück"]),
            new("🥣", "Schüssel", ["frühstück", "müsli", "suppe"]),
            new("🍲", "Topf", ["kochen", "suppe", "eintopf"]),
            new("🫕", "Fondue", ["kochen", "essen"]),
            new("🥗", "Salat", ["essen", "kochen", "gesund"]),
            new("🧁", "Muffin", ["backen", "kuchen"]),
            new("🎂", "Torte", ["backen", "geburtstag", "kuchen"]),
            new("🍕", "Pizza", ["essen", "kochen", "bestellen"]),
            new("🥪", "Sandwich", ["essen", "frühstück", "brot"]),
            new("🍞", "Brot", ["brot", "bäcker", "frühstück"]),
            new("☕", "Kaffee", ["kaffee", "frühstück", "trinken"]),
            new("🫖", "Teekanne", ["tee", "trinken"]),
            new("🥛", "Milch", ["milch", "trinken", "kühlschrank"]),
            new("🧃", "Saft", ["trinken", "kinder", "saft"]),
            new("🍶", "Flasche", ["trinken", "wasser"]),
            new("🔪", "Messer", ["kochen", "schneiden"]),
            new("🧑‍🍳", "Koch", ["kochen", "küche"]),
        ]),

        new("Garten & Natur", [
            new("🌿", "Kräuter", ["garten", "pflanzen", "kräuter"]),
            new("🌱", "Keimling", ["pflanzen", "garten", "säen"]),
            new("🪴", "Topfpflanze", ["pflanze", "gießen", "blume"]),
            new("🌻", "Sonnenblume", ["blume", "garten"]),
            new("🌷", "Tulpe", ["blume", "garten", "frühling"]),
            new("🌹", "Rose", ["blume", "garten"]),
            new("🌳", "Baum", ["baum", "garten", "hecke"]),
            new("🍂", "Herbstlaub", ["laub", "herbst", "harken"]),
            new("🍁", "Ahornblatt", ["laub", "herbst"]),
            new("🪨", "Stein", ["garten", "weg"]),
            new("🧑‍🌾", "Gärtner", ["garten", "pflanzen"]),
            new("🌊", "Welle", ["wasser", "pool", "gießen"]),
            new("💧", "Tropfen", ["wasser", "gießen"]),
            new("☀️", "Sonne", ["sonne", "sommer", "wetter"]),
            new("🌧️", "Regen", ["regen", "wetter"]),
            new("❄️", "Schnee", ["schnee", "winter", "kalt"]),
        ]),

        new("Tiere", [
            new("🐱", "Katze", ["katze", "kater", "tier"]),
            new("🐶", "Hund", ["hund", "tier", "gassi"]),
            new("🐾", "Pfote", ["tier", "futter", "pfote"]),
            new("🐟", "Fisch", ["fisch", "aquarium", "tier"]),
            new("🐹", "Hamster", ["hamster", "tier", "nager"]),
            new("🐰", "Hase", ["hase", "kaninchen", "tier"]),
            new("🦜", "Papagei", ["vogel", "tier"]),
            new("🐦", "Vogel", ["vogel", "tier"]),
            new("🐢", "Schildkröte", ["schildkröte", "tier"]),
            new("🦎", "Eidechse", ["reptil", "tier"]),
            new("🐴", "Pferd", ["pferd", "reiten"]),
        ]),

        new("Müll & Recycling", [
            new("🗑️", "Mülleimer", ["müll", "entsorgen", "wegwerfen"]),
            new("♻️", "Recycling", ["recycling", "trennen", "wertstoff"]),
            new("📦", "Paket", ["paket", "karton", "altpapier"]),
            new("📰", "Zeitung", ["papier", "altpapier", "zeitung"]),
            new("🫙", "Glas", ["altglas", "glas", "einmachen"]),
            new("🥫", "Dose", ["dose", "müll", "recycling"]),
            new("🔋", "Batterie", ["batterie", "entsorgen", "sondermüll"]),
        ]),

        new("Kinder & Schule", [
            new("📚", "Bücher", ["schule", "lesen", "lernen", "hausaufgaben"]),
            new("✏️", "Bleistift", ["schreiben", "hausaufgaben", "schule"]),
            new("🎒", "Rucksack", ["schule", "ranzen", "kinder"]),
            new("📐", "Geodreieck", ["mathe", "schule"]),
            new("🔬", "Mikroskop", ["wissenschaft", "schule"]),
            new("🎨", "Palette", ["malen", "kunst", "basteln"]),
            new("✂️", "Schere", ["basteln", "schneiden"]),
            new("🎮", "Controller", ["spielen", "gaming", "konsole"]),
            new("🧸", "Teddy", ["spielzeug", "kinder", "aufräumen"]),
            new("🎵", "Note", ["musik", "üben", "instrument"]),
            new("🎹", "Klavier", ["klavier", "üben", "musik"]),
            new("🎸", "Gitarre", ["gitarre", "üben", "musik"]),
            new("⚽", "Fußball", ["sport", "fußball", "spielen"]),
            new("🏀", "Basketball", ["sport", "basketball"]),
            new("🚴", "Radfahrer", ["fahrrad", "sport"]),
            new("🏊", "Schwimmer", ["schwimmen", "sport"]),
        ]),

        new("Einkauf & Finanzen", [
            new("🛒", "Einkaufswagen", ["einkaufen", "supermarkt"]),
            new("🛍️", "Einkaufstüte", ["einkaufen", "shopping"]),
            new("💰", "Geldsack", ["geld", "sparen", "credits"]),
            new("💳", "Kreditkarte", ["bezahlen", "geld", "karte"]),
            new("🧾", "Quittung", ["quittung", "rechnung", "einkauf"]),
            new("🏷️", "Preisschild", ["angebot", "preis", "einkauf"]),
            new("💶", "Euro", ["geld", "euro", "taschengeld"]),
        ]),

        new("Werkzeug & Auto", [
            new("🔧", "Schraubenschlüssel", ["reparieren", "werkzeug"]),
            new("🔨", "Hammer", ["werkzeug", "nagel", "reparieren"]),
            new("🪛", "Schraubenzieher", ["werkzeug", "schrauben"]),
            new("🪚", "Säge", ["sägen", "holz", "werkzeug"]),
            new("🧰", "Werkzeugkasten", ["werkzeug", "reparieren"]),
            new("🪜", "Leiter", ["leiter", "hoch", "lampe"]),
            new("🎨", "Farbe", ["streichen", "malen", "farbe"]),
            new("🖌️", "Pinsel", ["streichen", "malen"]),
            new("🚗", "Auto", ["auto", "fahren"]),
            new("⛽", "Tankstelle", ["tanken", "auto"]),
            new("🚙", "SUV", ["auto", "fahren"]),
            new("🚲", "Fahrrad", ["fahrrad", "rad"]),
            new("🛴", "Roller", ["roller", "kinder"]),
        ]),

        new("Gesundheit & Pflege", [
            new("💊", "Pille", ["medikament", "arzt", "gesundheit"]),
            new("🩹", "Pflaster", ["pflaster", "arzt", "verletzung"]),
            new("🦷", "Zahn", ["zähne", "zahnarzt", "putzen"]),
            new("🪥", "Zahnbürste", ["zähne", "putzen", "bad"]),
            new("💪", "Muskel", ["sport", "fitness", "stark"]),
            new("🏃", "Laufen", ["joggen", "sport", "laufen"]),
            new("🧘", "Yoga", ["yoga", "entspannung", "sport"]),
            new("😴", "Schlafen", ["schlafen", "bett", "nacht"]),
            new("🛌", "Im Bett", ["schlafen", "krank", "ausruhen"]),
        ]),

        new("Zeit & Wetter", [
            new("⏰", "Wecker", ["wecker", "aufstehen", "zeit"]),
            new("📅", "Kalender", ["termin", "kalender", "planen"]),
            new("⏱️", "Stoppuhr", ["timer", "zeit", "sport"]),
            new("⌛", "Sanduhr", ["warten", "zeit", "geduld"]),
            new("🌡️", "Thermometer", ["temperatur", "wetter", "fieber"]),
            new("🌈", "Regenbogen", ["wetter", "schön"]),
        ]),
    ];

    /// <summary>
    /// All emojis as a flat list.
    /// </summary>
    public static readonly Lazy<IReadOnlyList<EmojiEntry>> AllEmojis = new(() =>
        Categories.SelectMany(c => c.Emojis).ToList());

    /// <summary>
    /// Search emojis by German keyword.
    /// </summary>
    public static List<EmojiEntry> Search(string query, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var q = query.Trim().ToLowerInvariant();
        var qNorm = IconSearchIndex.NormalizeUmlauts(q);
        var results = new List<(EmojiEntry entry, int score)>();

        foreach (var entry in AllEmojis.Value)
        {
            // Match on name
            var nameNorm = IconSearchIndex.NormalizeUmlauts(entry.Name.ToLowerInvariant());
            if (entry.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || nameNorm.Contains(qNorm))
            {
                var score = entry.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                    || nameNorm.StartsWith(qNorm) ? 100 : 60;
                results.Add((entry, score));
                continue;
            }

            // Match on keywords (with umlaut tolerance)
            var keywordScore = 0;
            foreach (var kw in entry.Keywords)
            {
                var kwNorm = IconSearchIndex.NormalizeUmlauts(kw);
                if (kw == q || kwNorm == qNorm) { keywordScore = 90; break; }
                if (kw.StartsWith(q) || kwNorm.StartsWith(qNorm)) { keywordScore = Math.Max(keywordScore, 70); }
                if (kw.Contains(q) || kwNorm.Contains(qNorm)) { keywordScore = Math.Max(keywordScore, 40); }
            }

            if (keywordScore > 0)
            {
                results.Add((entry, keywordScore));
            }
        }

        return results
            .OrderByDescending(r => r.score)
            .Take(maxResults)
            .Select(r => r.entry)
            .ToList();
    }
}

/// <summary>
/// A category of emojis.
/// </summary>
public record EmojiCategory(string Name, IReadOnlyList<EmojiEntry> Emojis);

/// <summary>
/// A single emoji entry with metadata.
/// </summary>
public record EmojiEntry(string Emoji, string Name, IReadOnlyList<string> Keywords);
