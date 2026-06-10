using FamilyAssistant.Services;

namespace FamilyAssistant.Tests;

public class RecipeStepIngredientMatcherTests
{
    [Fact]
    public void ResolveStepIngredientIds_UsesInstructionWhenAiNamesAreEmpty()
    {
        var ingredientNames = new[] { "Cervelat", "Emmentaler", "Zwiebeln", "Essiggurken" };

        var selectedIds = RecipeStepIngredientMatcher.ResolveStepIngredientIds(
            ingredientNames,
            [],
            "Cervelat, Emmentaler, Zwiebeln und Essiggurken in feine Streifen schneiden.");

        Assert.Equal([1, 2, 3, 4], selectedIds.OrderBy(id => id));
    }

    [Fact]
    public void ResolveStepIngredientIds_MatchesDistinctIngredientTokens()
    {
        var ingredientNames = new[] { "große Eier", "Cherrytomaten", "Feta oder Sesam" };

        var selectedIds = RecipeStepIngredientMatcher.ResolveStepIngredientIds(
            ingredientNames,
            [],
            "Ei verquirlen, Tomaten halbieren und mit Sesam bestreuen.");

        Assert.Equal([1, 2, 3], selectedIds.OrderBy(id => id));
    }

    [Fact]
    public void ResolveStepIngredientIds_KeepsImportedNameMatching()
    {
        var ingredientNames = new[] { "gekochte Kichererbsen", "Sesampaste", "Knoblauch" };

        var selectedIds = RecipeStepIngredientMatcher.ResolveStepIngredientIds(
            ingredientNames,
            ["Kichererbsen", "Knoblauchzehen"],
            "Alles in den Mixtopf geben.");

        Assert.Equal([1, 3], selectedIds.OrderBy(id => id));
    }
}
