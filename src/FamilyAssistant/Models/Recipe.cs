namespace FamilyAssistant.Models;

public class Recipe
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public decimal BaseServings { get; set; } = 4;

    public string? SourceUrl { get; set; }

    public string? SourceName { get; set; }

    public RecipeSourceType SourceType { get; set; } = RecipeSourceType.Manual;

    public string? SourceText { get; set; }

    public int? TotalTimeMinutes { get; set; }

    public int? WorkTimeMinutes { get; set; }

    public int? CookTimeMinutes { get; set; }

    public int? RestTimeMinutes { get; set; }

    public string? Difficulty { get; set; }

    public int? CategoryId { get; set; }
    public RecipeCategory? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RecipeTag> Tags { get; set; } = [];
    public ICollection<RecipeIngredientGroup> IngredientGroups { get; set; } = [];
    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
    public ICollection<RecipeStep> Steps { get; set; } = [];
    public ICollection<RecipeImage> Images { get; set; } = [];
}
