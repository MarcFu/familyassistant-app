namespace FamilyAssistant.Models;

public class RecipeStepIngredient
{
    public int RecipeStepId { get; set; }
    public RecipeStep RecipeStep { get; set; } = null!;

    public int RecipeIngredientId { get; set; }
    public RecipeIngredient RecipeIngredient { get; set; } = null!;
}
