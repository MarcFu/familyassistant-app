namespace FamilyAssistant.Models;

public class RecipeStepImage
{
    public int Id { get; set; }

    public int RecipeStepId { get; set; }
    public RecipeStep RecipeStep { get; set; } = null!;

    public string? SourceUrl { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public string? FilePath { get; set; }

    public long? FileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
