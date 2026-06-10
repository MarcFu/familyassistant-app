namespace FamilyAssistant.Models;

public class RecipeIngredientGroup
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public required string Name { get; set; }

    public int SortOrder { get; set; }

    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
}
