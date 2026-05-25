using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Services;

/// <summary>
/// Holds the current user context for the session.
/// In Add-on mode: resolved from X-Remote-User-Id header.
/// In Dev mode: manually set via /dev page.
/// </summary>
public class UserContextService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserContextService> _logger;

    // Persisted across the circuit (Blazor Server = scoped per circuit)
    private int? _currentPersonId;
    private Person? _currentPerson;

    public event Action? CurrentPersonChanged;

    public UserContextService(IServiceScopeFactory scopeFactory, ILogger<UserContextService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// The currently active person, or null if no user context is set (Guest).
    /// </summary>
    public Person? CurrentPerson => _currentPerson;

    /// <summary>
    /// The current person's ID, or null.
    /// </summary>
    public int? CurrentPersonId => _currentPersonId;

    /// <summary>
    /// Whether the current user is authenticated (has a person record).
    /// </summary>
    public bool IsAuthenticated => _currentPerson is not null;

    /// <summary>
    /// Whether the current user can perform actions (not a guest, not paused).
    /// </summary>
    public bool CanAct => _currentPerson is not null
        && _currentPerson.Role != PersonRole.Guest
        && !_currentPerson.IsPaused;

    /// <summary>
    /// Whether the current user is a Parent or Admin.
    /// </summary>
    public bool IsParentOrAdmin => _currentPerson?.Role is PersonRole.Parent or PersonRole.Admin;

    /// <summary>
    /// Whether the current user is an Admin.
    /// </summary>
    public bool IsAdmin => _currentPerson?.Role is PersonRole.Admin;

    /// <summary>
    /// Set the current user by Person ID (used by Dev page).
    /// </summary>
    public async Task SetCurrentPersonAsync(int? personId)
    {
        _currentPersonId = personId;
        if (personId is null)
        {
            _currentPerson = null;
            CurrentPersonChanged?.Invoke();
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _currentPerson = await db.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId);
        CurrentPersonChanged?.Invoke();
    }

    /// <summary>
    /// Resolve the current user from HA Ingress header (X-Remote-User-Id).
    /// Matches on Person.HaUserId (the HA user UUID).
    /// </summary>
    public async Task ResolveFromHaUserIdAsync(string? haUserId)
    {
        if (string.IsNullOrEmpty(haUserId))
        {
            _currentPerson = null;
            _currentPersonId = null;
            CurrentPersonChanged?.Invoke();
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _currentPerson = await db.Persons.AsNoTracking()
            .FirstOrDefaultAsync(p => p.HaUserId == haUserId);

        _currentPersonId = _currentPerson?.Id;

        if (_currentPerson is null)
        {
            _logger.LogInformation("HA user '{UserId}' not mapped to any person — treating as Guest", haUserId);
        }
        else
        {
            _logger.LogDebug("Resolved HA user '{UserId}' to person '{Name}' (Role: {Role})",
                haUserId, _currentPerson.Name, _currentPerson.Role);
        }

        CurrentPersonChanged?.Invoke();
    }

    /// <summary>
    /// Refresh the current person data from DB (e.g., after credits change).
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_currentPersonId is not null)
        {
            await SetCurrentPersonAsync(_currentPersonId);
        }
    }
}
