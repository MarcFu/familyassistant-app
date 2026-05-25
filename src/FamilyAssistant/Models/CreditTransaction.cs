namespace FamilyAssistant.Models;

public class CreditTransaction
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>
    /// Positive = earned, Negative = spent
    /// </summary>
    public int Amount { get; set; }

    public required string Reason { get; set; }

    public CreditTransactionType Type { get; set; }

    /// <summary>
    /// If credits were earned from completing a task
    /// </summary>
    public int? ChoreTaskId { get; set; }
    public ChoreTask? ChoreTask { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum CreditTransactionType
{
    /// <summary>
    /// Earned by completing a chore
    /// </summary>
    ChoreCompleted,

    /// <summary>
    /// Spent on extra internet time
    /// </summary>
    InternetTimeSpent,

    /// <summary>
    /// Manually added by parent (bonus)
    /// </summary>
    ManualBonus,

    /// <summary>
    /// Manually deducted by parent (penalty)
    /// </summary>
    ManualPenalty
}
