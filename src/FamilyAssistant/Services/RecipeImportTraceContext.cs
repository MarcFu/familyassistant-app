namespace FamilyAssistant.Services;

public sealed record RecipeImportTraceContext(int CandidateId, int AttemptNumber, bool Enabled);
