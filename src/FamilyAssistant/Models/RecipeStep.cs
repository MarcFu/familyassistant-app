namespace FamilyAssistant.Models;

public class RecipeStep
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public string Instruction { get; set; } = "";

    public string? InstructionMarkup { get; set; }

    public string? Section { get; set; }

    public int GroupNumber { get; set; } = 1;

    public int SortOrder { get; set; }

    public ICollection<RecipeStepIngredient> Ingredients { get; set; } = [];
    public ICollection<RecipeStepImage> Images { get; set; } = [];
}
