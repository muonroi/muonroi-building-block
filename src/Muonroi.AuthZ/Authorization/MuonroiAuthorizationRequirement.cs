namespace Muonroi.AuthZ.Authorization;

/// <summary>
/// ASP.NET Core authorization requirement backed by the Muonroi Rule Engine.
/// Register a policy with this requirement and rules are evaluated at runtime.
/// </summary>
public sealed class MuonroiAuthorizationRequirement(string resource, string action)
    : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the protected resource name.
    /// </summary>
    public string Resource { get; } = resource;

    /// <summary>
    /// Gets the requested action name.
    /// </summary>
    public string Action { get; } = action;
}
