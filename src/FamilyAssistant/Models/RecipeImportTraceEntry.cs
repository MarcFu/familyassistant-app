namespace FamilyAssistant.Models;

public class RecipeImportTraceEntry
{
    public int Id { get; set; }

    public int CandidateId { get; set; }
    public RecipeImportCandidate Candidate { get; set; } = null!;

    public int AttemptNumber { get; set; }

    public int SortOrder { get; set; }

    public string StepName { get; set; } = "";

    public string Stage { get; set; } = "";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public long? DurationMs { get; set; }

    public string Status { get; set; } = "Running";

    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public string? ErrorMessage { get; set; }
}
