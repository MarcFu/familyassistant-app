using System.ComponentModel.DataAnnotations;

namespace FamilyAssistant.Models;

/// <summary>
/// Persisted record of an achievement unlocked by a person.
/// Tracks when it was unlocked and whether the unlock animation has been shown.
/// </summary>
public class PersonAchievement
{
    public int Id { get; set; }

    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>
    /// The achievement definition key (e.g. "first_task", "streak_7").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AchievementKey { get; set; } = "";

    /// <summary>
    /// When this achievement was first unlocked.
    /// </summary>
    public DateTime UnlockedAt { get; set; }

    /// <summary>
    /// Whether the unlock animation/reveal has been shown to the user.
    /// false = pending reveal (show animation next time user visits).
    /// </summary>
    public bool Revealed { get; set; }
}
