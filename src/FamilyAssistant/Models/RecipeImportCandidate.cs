namespace FamilyAssistant.Models;

public class RecipeImportCandidate
{
    public int Id { get; set; }

    public RecipeSourceType SourceType { get; set; }

    public string? SourceUrl { get; set; }

    public string? SourceText { get; set; }

    public string TargetLanguage { get; set; } = "en";

    public RecipeImportQuality Quality { get; set; } = RecipeImportQuality.HighAi;

    public RecipeImportCandidateStatus Status { get; set; } = RecipeImportCandidateStatus.Requested;

    public int ProgressPercent { get; set; }

    public string? ProgressMessage { get; set; }

    public string? ProposalName { get; set; }

    public string? DraftJson { get; set; }

    public string? QualityReport { get; set; }

    public string? ErrorMessage { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? HeartbeatAt { get; set; }

    public DateTime? PausedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? LockedBy { get; set; }

    public bool PauseRequested { get; set; }

    public ICollection<RecipeImportTraceEntry> TraceEntries { get; set; } = [];
}
