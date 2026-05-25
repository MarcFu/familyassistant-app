namespace FamilyAssistant.Services;

/// <summary>
/// Evaluates achievement unlock state and profile stats for a given person.
/// </summary>
public interface IAchievementService
{
    IReadOnlyList<AchievementDefinition> Definitions { get; }
    Task<AchievementEvaluation> EvaluateAsync(int personId);
}

public sealed record AchievementDefinition(
    string Key,
    string Name,
    string Category,
    string Condition,
    string Tier,
    string Icon,
    bool IsHidden = false);

public sealed record AchievementEvaluation(
    HashSet<string> UnlockedKeys,
    Dictionary<string, string> Facts,
    ProfileStats Stats);

public sealed record ProfileStats(
    int TotalCompleted,
    int CurrentStreak,
    int LongestStreak,
    int UnlockedBadges,
    int TotalBadges);
