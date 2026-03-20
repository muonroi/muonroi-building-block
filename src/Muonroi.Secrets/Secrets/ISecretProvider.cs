namespace Muonroi.Secrets.Secrets;

/// <summary>
/// Provides access to named secrets.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Gets a secret by name.
    /// </summary>
    /// <param name="name">Secret key.</param>
    /// <returns>The secret value or null if not found.</returns>
    string? GetSecret(string name);
}
