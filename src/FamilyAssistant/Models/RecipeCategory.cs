namespace FamilyAssistant.Models;

public class RecipeCategory
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int? ParentCategoryId { get; set; }
    public RecipeCategory? ParentCategory { get; set; }

    public ICollection<RecipeCategory> Children { get; set; } = [];
    public ICollection<Recipe> Recipes { get; set; } = [];
}
