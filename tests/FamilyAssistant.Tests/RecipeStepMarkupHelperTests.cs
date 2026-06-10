using FamilyAssistant.Services;

namespace FamilyAssistant.Tests;

public class RecipeStepMarkupHelperTests
{
    [Fact]
    public void StripMarkers_RemovesIngredientMarkersAndNormalizesWhitespace()
    {
        var text = RecipeStepMarkupHelper.StripMarkers("Die Zwiebel {{ingredient:abc}} fein würfeln .");

        Assert.Equal("Die Zwiebel fein würfeln.", text);
    }

    [Fact]
    public void CleanMarkup_RemovesUnknownMarkers()
    {
        var markup = RecipeStepMarkupHelper.CleanMarkup(
            "Zwiebel {{ingredient:known}} und Knoblauch {{ingredient:unknown}} schneiden.",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "known" });

        Assert.Equal("Zwiebel {{ingredient:known}} und Knoblauch schneiden.", markup);
    }

    [Fact]
    public void ConvertIngredientNameMarkersToTokens_ConvertsKnownIngredientNames()
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rote Zwiebel"] = "token1"
        };

        var markup = RecipeStepMarkupHelper.ConvertIngredientNameMarkersToTokens(
            "Die Zwiebel {{ingredient:rote Zwiebel}} fein würfeln.",
            tokens);

        Assert.Equal("Die Zwiebel {{ingredient:token1}} fein würfeln.", markup);
    }
}
