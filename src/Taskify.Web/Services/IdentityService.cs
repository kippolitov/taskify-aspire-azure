using Taskify.Shared.Dtos;

namespace Taskify.Web.Services;

/// <summary>
/// Holds the currently selected user for this Blazor circuit.
/// State is lost on page refresh — no persistence required in Phase 1.
/// </summary>
public class IdentityService
{
    private UserDto? _currentUser;

    public UserDto? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser is not null;

    public event Action? OnChange;

    public void SetUser(UserDto user)
    {
        _currentUser = user;
        OnChange?.Invoke();
    }

    public void ClearUser()
    {
        _currentUser = null;
        OnChange?.Invoke();
    }
}
