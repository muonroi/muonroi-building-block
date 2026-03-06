namespace Muonroi.Core.Abstractions.Interfaces;

public static class UserContext
{
    private static readonly AsyncLocal<string?> _currentUserGuid = new();
    private static readonly AsyncLocal<string?> _currentUsername = new();

    public static string? CurrentUserGuid
    {
        get => _currentUserGuid.Value;
        set => _currentUserGuid.Value = value;
    }

    public static string? CurrentUsername
    {
        get => _currentUsername.Value;
        set => _currentUsername.Value = value;
    }
}
