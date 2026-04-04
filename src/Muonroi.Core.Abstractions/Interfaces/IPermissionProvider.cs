namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Provides a collection of permission definitions.
/// </summary>
public interface IPermissionProvider
{
    /// <summary>
    /// Gets all defined permissions.
    /// </summary>
    /// <returns>An enumerable collection of permission definitions.</returns>
    IEnumerable<PermissionDefinition> GetPermissions();
}
