using FamilyAssistant.Models;

namespace FamilyAssistant.Services;

/// <summary>
/// Evaluates achievement unlock state and profile stats for a given person.
/// Persists unlock timestamps and manages reveal state.
/// </summary>
public interface IAchievementService
{
    IReadOnlyList<AchievementDefinition> Definitions { get; }

    /// <summary>
    /// Evaluates all achievements for a person, persists newly unlocked ones,
    /// and returns the full evaluation result.
    /// </summary>
    Task<AchievementEvaluation> EvaluateAsync(int personId);

    /// <summary>
    /// Gets all unrevealed achievements for a person (for the unlock animation).
    /// </summary>
    Task<IReadOnlyList<PersonAchievement>> GetUnrevealedAsync(int personId);

    /// <summary>
    /// Marks achievements as revealed (after the animation has been shown).
    /// </summary>
    Task MarkRevealedAsync(IEnumerable<int> achievementIds);

    /// <summary>
    /// Gets all persisted achievements for a person (with timestamps).
    /// </summary>
    Task<IReadOnlyList<PersonAchievement>> GetAllForPersonAsync(int personId);
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
    ProfileStats Stats,
    IReadOnlyList<PersonAchievement> NewlyUnlocked);

public sealed record ProfileStats(
    int TotalCompleted,
    int CurrentStreak,
    int LongestStreak,
    int UnlockedBadges,
    int TotalBadges);
