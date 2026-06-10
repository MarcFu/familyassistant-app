namespace FamilyAssistant.Models;

public class RecipeTag
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public ICollection<Recipe> Recipes { get; set; } = [];
}
