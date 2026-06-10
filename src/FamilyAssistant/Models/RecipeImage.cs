namespace FamilyAssistant.Models;

public class RecipeImage
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "image/jpeg";

    public string FilePath { get; set; } = "";

    public long FileSize { get; set; }

    public bool IsPrimary { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
