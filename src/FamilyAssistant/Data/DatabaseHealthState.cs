namespace FamilyAssistant.Data;

/// <summary>
/// Holds the database migration health state.
/// If migration fails at startup, the error is stored here
/// and a middleware serves a static error page for all requests.
/// </summary>
public sealed class DatabaseHealthState
{
    public bool HasMigrationError { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorDetails { get; private set; }

    public void SetError(Exception ex)
    {
        HasMigrationError = true;
        ErrorMessage = ex.Message;
        ErrorDetails = ex.ToString();
    }
}
