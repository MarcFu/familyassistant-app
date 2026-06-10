namespace FamilyAssistant.Models;

public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int? GroupId { get; set; }
    public RecipeIngredientGroup? Group { get; set; }

    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    public required string Name { get; set; }

    public string? Note { get; set; }

    public string MarkerToken { get; set; } = Guid.NewGuid().ToString("N");

    public int SortOrder { get; set; }

    public ICollection<RecipeStepIngredient> StepLinks { get; set; } = [];
}
