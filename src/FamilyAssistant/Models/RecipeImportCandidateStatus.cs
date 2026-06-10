namespace FamilyAssistant.Models;

public enum RecipeImportCandidateStatus
{
    Requested = 0,
    Fetching = 1,
    Parsing = 2,
    EnhancingWithAi = 3,
    ProposalReady = 4,
    Failed = 5,
    Cancelled = 6,
    Accepted = 7,
    Paused = 8,
    Interrupted = 9
}
