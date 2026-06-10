namespace FamilyAssistant.Models;

public class RecipeImportDraft
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public decimal BaseServings { get; set; } = 4;
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceName { get; set; }
    public RecipeSourceType SourceType { get; set; } = RecipeSourceType.Url;
    public string? SourceText { get; set; }
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = [];
    public int? TotalTimeMinutes { get; set; }
    public int? WorkTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? RestTimeMinutes { get; set; }
    public string? Difficulty { get; set; }
    public string? QualityReport { get; set; }
    public List<RecipeIngredientDraft> Ingredients { get; set; } = [];
    public List<RecipeStepDraft> Steps { get; set; } = [];
}

public class RecipeIngredientDraft
{
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string Name { get; set; } = "";
    public string? Note { get; set; }
    public string? OriginalText { get; set; }
    public string? GroupName { get; set; }
}

public class RecipeStepDraft
{
    public string Instruction { get; set; } = "";
    public string? InstructionMarkup { get; set; }
    public string? Section { get; set; }
    public int GroupNumber { get; set; } = 1;
    public string? ImageUrl { get; set; }
    public List<string> IngredientNames { get; set; } = [];
}
