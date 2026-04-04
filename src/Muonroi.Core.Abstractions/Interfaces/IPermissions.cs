namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Represents a set of permissions.
/// </summary>
public interface IPermissions
{
    /// <summary>
    /// Converts the permissions to a long value representation.
    /// </summary>
    /// <returns>A long value representing the permissions.</returns>
    long ToLong();
}
