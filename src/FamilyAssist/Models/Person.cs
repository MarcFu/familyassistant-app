namespace FamilyAssist.Models;

public class Person
{
    public int Id { get; set; }

    /// <summary>
    /// The Home Assistant person entity ID (e.g., "person.alex")
    /// </summary>
    public required string HaEntityId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Role in the household system
    /// </summary>
    public PersonRole Role { get; set; } = PersonRole.Child;

    /// <summary>
    /// Current credit balance
    /// </summary>
    public int Credits { get; set; }

    /// <summary>
    /// HA switch entity IDs that control internet access for this person's devices
    /// </summary>
    public List<string> DeviceEntities { get; set; } = [];

    /// <summary>
    /// HA todo list entity ID for syncing tasks (e.g., "todo.alex_tasks")
    /// </summary>
    public string? HaTodoEntityId { get; set; }

    /// <summary>
    /// Relative path to the entity picture in HA (e.g., "/api/image/serve/xxx/512x512")
    /// </summary>
    public string? EntityPicture { get; set; }

    /// <summary>
    /// Whether this person is currently paused (vacation etc.)
    /// </summary>
    public bool IsPaused { get; set; }

    public ICollection<CreditTransaction> CreditTransactions { get; set; } = [];
    public ICollection<InternetRule> InternetRules { get; set; } = [];
    public ICollection<ChoreSchedule> DefaultSchedules { get; set; } = [];
    public ICollection<ChoreTask> ClaimedTasks { get; set; } = [];
}

public enum PersonRole
{
    /// <summary>
    /// Full system access, HA God-User
    /// </summary>
    Admin,

    /// <summary>
    /// Can confirm tasks, manipulate credits, complete tasks without confirmation
    /// </summary>
    Parent,

    /// <summary>
    /// Can claim/complete tasks, needs parent confirmation for credits
    /// </summary>
    Child,

    /// <summary>
    /// Read-only access. For HA users not managed in the system (guests, visitors).
    /// Can view tasks/dashboard but cannot claim, complete, or modify anything.
    /// </summary>
    Guest
}
