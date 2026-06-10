using FamilyAssistant.Services;
using FamilyAssistant.Models;

namespace FamilyAssistant.Tests;

public class RecipeImportServiceTests
{
    [Fact]
    public void ImportCandidates_AreRecordedForRegressionTests()
    {
        Assert.StartsWith("https://www.chefkoch.de/", RecipeImportTestData.ChefkochSchweizerWurstsalatUrl);
        Assert.StartsWith("https://emmikochteinfach.de/", RecipeImportTestData.EmmiSchweizerWurstsalatUrl);
        Assert.StartsWith("https://www.kochwiki.org/", RecipeImportTestData.KochwikiSchweizerWurstsalatUrl);
        Assert.StartsWith("https://www.rezeptwelt.de/", RecipeImportTestData.RezeptweltHummusMitTahinaUrl);
    }

    [Fact]
    public void ParseJsonLdForTests_ParsesBasicRecipeObject()
    {
        const string json = """
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Pfannkuchen",
          "description": "Einfacher Teig",
          "recipeYield": "4 Portionen",
          "image": "https://example.test/pancake.jpg",
          "recipeIngredient": [
            "550 g Mehl",
            "5 Eier",
            "1 Prise Salz",
            "Pfeffer nach Geschmack"
          ],
          "recipeInstructions": [
            { "@type": "HowToStep", "text": "Mehl und Eier verrühren." },
            { "@type": "HowToStep", "text": "In der Pfanne ausbacken." }
          ]
        }
        """;

        var draft = RecipeImportService.ParseJsonLdForTests(json, new Uri("https://example.test/rezept"));

        Assert.Equal("Pfannkuchen", draft.Name);
        Assert.Equal("Einfacher Teig", draft.Description);
        Assert.Equal(4m, draft.BaseServings);
        Assert.Equal("https://example.test/pancake.jpg", draft.ImageUrl);
        Assert.Equal(4, draft.Ingredients.Count);
        Assert.Equal(550m, draft.Ingredients[0].Quantity);
        Assert.Equal("g", draft.Ingredients[0].Unit);
        Assert.Equal("Mehl", draft.Ingredients[0].Name);
        Assert.Equal(5m, draft.Ingredients[1].Quantity);
        Assert.Equal("Eier", draft.Ingredients[1].Name);
        Assert.Equal(2, draft.Steps.Count);
    }

    [Fact]
    public void ParseJsonLdForTests_DoesNotTreatAdjectiveStartAsUnit()
    {
        const string json = """
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Eierkuchen",
          "recipeIngredient": [
            "1-2 große Eier"
          ],
          "recipeInstructions": [
            "Eier verquirlen."
          ]
        }
        """;

        var draft = RecipeImportService.ParseJsonLdForTests(json, new Uri("https://example.test/rezept"));

        Assert.Single(draft.Ingredients);
        Assert.Equal(1m, draft.Ingredients[0].Quantity);
        Assert.Null(draft.Ingredients[0].Unit);
        Assert.Equal("große Eier", draft.Ingredients[0].Name);
    }

    [Fact]
    public void ParseJsonLdForTests_FindsRecipeInsideGraphAndResolvesRelativeImage()
    {
        const string json = """
        {
          "@graph": [
            { "@type": "WebPage", "name": "Page" },
            {
              "@type": ["Recipe", "Thing"],
              "name": "Kuchen",
              "recipeYield": "8 Stück",
              "image": { "url": "/images/cake.jpg" },
              "recipeIngredient": "200 g Zucker",
              "recipeInstructions": {
                "@type": "HowToSection",
                "itemListElement": [
                  { "@type": "HowToStep", "text": "Alles mischen." }
                ]
              }
            }
          ]
        }
        """;

        var draft = RecipeImportService.ParseJsonLdForTests(json, new Uri("https://example.test/recipes/cake"));

        Assert.Equal("Kuchen", draft.Name);
        Assert.Equal(8m, draft.BaseServings);
        Assert.Equal("https://example.test/images/cake.jpg", draft.ImageUrl);
        Assert.Single(draft.Ingredients);
        Assert.Single(draft.Steps);
    }

    [Fact]
    public void ParseTextForTests_ParsesForwardedWhatsAppRecipe()
    {
        var draft = RecipeImportService.ParseTextForTests(RecipeImportTestData.WhatsAppChickenBroccoliBowl, RecipeSourceType.WhatsApp);

        Assert.Equal("Hähnchen-Brokkoli-Bowl mit Joghurt-Dip", draft.Name);
        Assert.Equal(RecipeSourceType.WhatsApp, draft.SourceType);
        Assert.Equal("WhatsApp", draft.SourceName);
        Assert.Equal(2m, draft.BaseServings);
        Assert.Contains("Low Carb Version", draft.Tags);
        Assert.Equal(12, draft.Ingredients.Count);
        Assert.Equal(2m, draft.Ingredients[0].Quantity);
        Assert.Equal("Hähnchenbrustfilets", draft.Ingredients[0].Name);
        Assert.Equal(150m, draft.Ingredients[3].Quantity);
        Assert.Equal("g", draft.Ingredients[3].Unit);
        Assert.Equal("Cherrytomaten", draft.Ingredients[3].Name);
        Assert.Equal("optional", draft.Ingredients[10].Note);
        Assert.Equal("Feta oder Sesam", draft.Ingredients[10].Name);
        Assert.Equal(8, draft.Steps.Count);
        Assert.Contains("proteinreich", draft.Notes);
        Assert.Equal(RecipeImportTestData.WhatsAppChickenBroccoliBowl.Trim(), draft.SourceText);
    }

    [Fact]
    public void ParseHtmlMicrodataForTests_ParsesRezeptweltMicrodata()
    {
        const string html = """
        <html>
        <head>
            <meta name="description" content="Hummus mit Tahina, ein Rezept der Kategorie Saucen/Dips/Brotaufstriche." />
            <meta property="og:image" content="https://images.example/hummus.jpg" />
        </head>
        <body>
            <div itemscope itemtype="https://schema.org/Recipe">
                <span itemprop="recipeCategory">Saucen/Dips/Brotaufstriche</span>
                <meta itemprop="name" content="Hummus mit Tahina">
                <meta itemprop="image" content="https://images.example/original/hummus.jpg">
                <meta itemprop="prepTime" content="PT5M">
                <meta itemprop="totalTime" content="PT5M">
                <span itemprop="recipeYield"> 0 </span>
                <div>Schwierigkeitsgrad</div><div class="fw-bold">einfach</div>
                <ul>
                    <li itemprop="recipeIngredient"><span>1</span><span> Dose</span><span> gekochte Kichererbsen, </span><span>400 g</span></li>
                    <li itemprop="recipeIngredient"><span>4 - 6</span><span> Zehen</span><span> Knoblauch, </span><span>je nach Geschmack</span></li>
                    <li itemprop="recipeIngredient"><span>3</span><span> Esslöffel</span><span> Sesampaste, (Tahina)</span></li>
                </ul>
                <span itemprop="text">Die Kichererbsen absch&uuml;tten.<br />Restliche Zutaten hinzugeben.</span>
                <div itemprop="recipeHint">Dazu reicht man Fladenbrot.</div>
            </div>
        </body>
        </html>
        """;

        var draft = RecipeImportService.ParseHtmlMicrodataForTests(html, new Uri(RecipeImportTestData.RezeptweltHummusMitTahinaUrl));

        Assert.Equal("Hummus mit Tahina", draft.Name);
        Assert.Equal("Saucen/Dips/Brotaufstriche", draft.CategoryName);
        Assert.Equal(4m, draft.BaseServings);
        Assert.Equal("https://images.example/original/hummus.jpg", draft.ImageUrl);
        Assert.Equal(5, draft.WorkTimeMinutes);
        Assert.Equal(5, draft.TotalTimeMinutes);
        Assert.Equal("einfach", draft.Difficulty);
        Assert.Equal(RecipeSourceType.Url, draft.SourceType);
        Assert.Equal("www.rezeptwelt.de", draft.SourceName);
        Assert.Equal(3, draft.Ingredients.Count);
        Assert.Equal(1m, draft.Ingredients[0].Quantity);
        Assert.Equal("Dose", draft.Ingredients[0].Unit);
        Assert.Contains("Kichererbsen", draft.Ingredients[0].Name);
        Assert.Equal(2, draft.Steps.Count);
        Assert.Equal("Die Kichererbsen abschütten.", draft.Steps[0].Instruction);
        Assert.Equal("Restliche Zutaten hinzugeben.", draft.Steps[1].Instruction);
        Assert.Contains("Fladenbrot", draft.Notes);
    }

    [Fact]
    public void ParseHtmlMicrodataForTests_ParsesMediaWikiRecipeFallback()
    {
        const string html = """
        <html>
        <head>
            <title>Schweizer Wurstsalat – Koch-Wiki</title>
            <meta property="og:image" content="https://www.kochwiki.org/images/salat.jpg">
        </head>
        <body>
            <h1 id="firstHeading"><span>Schweizer Wurstsalat</span></h1>
            <table class="rztable">
                <tr><td>Zutatenmenge für:</td><td>2–3 Personen</td></tr>
                <tr><td>Zeitbedarf:</td><td>1 Stunde + 1 Stunde ziehen lassen</td></tr>
                <tr><td>Schwierigkeitsgrad:</td><td><img alt="leicht" src="leicht.gif"></td></tr>
            </table>
            <h2><span class="mw-headline" id="Zutaten">Zutaten</span></h2>
            <ul>
                <li>Marinade
                    <ul>
                        <li>250 ml Wasser</li>
                        <li>1 TL Senf</li>
                    </ul>
                </li>
                <li>Salatfertigung
                    <ul>
                        <li>350 g Cervelat</li>
                        <li>200 g Emmentaler</li>
                    </ul>
                </li>
            </ul>
            <h2><span class="mw-headline" id="Zubereitung">Zubereitung</span></h2>
            <h3><span class="mw-headline" id="Marinade">Marinade</span></h3>
            <ul>
                <li>Das Wasser in einem Topf zum Kochen bringen.</li>
                <li>Öl und Senf dazurühren.</li>
            </ul>
            <h3><span class="mw-headline" id="Salatfertigung">Salatfertigung:</span></h3>
            <ul>
                <li>Die Wurst in Stifte schneiden.</li>
            </ul>
            <h2><span class="mw-headline" id="Tipp">Tipp</span></h2>
            <ul><li>Je feiner die Wurststifte sind, desto kräftiger wird der Geschmack.</li></ul>
            <div id="catlinks"><div><a>Kategorien</a><ul><li><a>Schweizer Küche</a></li><li><a>Wurstsalat</a></li></ul></div></div>
        </body>
        </html>
        """;

        var draft = RecipeImportService.ParseHtmlMicrodataForTests(html, new Uri(RecipeImportTestData.KochwikiSchweizerWurstsalatUrl));

        Assert.Equal("Schweizer Wurstsalat", draft.Name);
        Assert.Equal(2m, draft.BaseServings);
        Assert.Equal(120, draft.TotalTimeMinutes);
        Assert.Equal("leicht", draft.Difficulty);
        Assert.Equal("https://www.kochwiki.org/images/salat.jpg", draft.ImageUrl);
        Assert.Equal(4, draft.Ingredients.Count);
        Assert.Equal("Marinade", draft.Ingredients[0].GroupName);
        Assert.Equal("Wasser", draft.Ingredients[0].Name);
        Assert.Equal("Salatfertigung", draft.Ingredients[2].GroupName);
        Assert.Equal(3, draft.Steps.Count);
        Assert.Equal("Marinade", draft.Steps[0].Section);
        Assert.Equal("Salatfertigung", draft.Steps[2].Section);
        Assert.Contains("Schweizer Küche", draft.Tags);
        Assert.Contains("Wurststifte", draft.Notes);
    }
}
