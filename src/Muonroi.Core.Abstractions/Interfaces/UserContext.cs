namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Provides a static context for the current user's identification.
/// </summary>
public static class UserContext
{
    private static readonly AsyncLocal<string?> _currentUserGuid = new();
    private static readonly AsyncLocal<string?> _currentUsername = new();

    /// <summary>
    /// Gets or sets the unique identifier (GUID) of the current user.
    /// </summary>
    public static string? CurrentUserGuid
    {
        get => _currentUserGuid.Value;
        set => _currentUserGuid.Value = value;
    }

    /// <summary>
    /// Gets or sets the username of the current user.
    /// </summary>
    public static string? CurrentUsername
    {
        get => _currentUsername.Value;
        set => _currentUsername.Value = value;
    }
}
