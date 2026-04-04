

namespace Muonroi.Secrets.Secrets;

/// <summary>
/// Secret provider backed by <see cref="IConfiguration"/>.
/// </summary>
/// <param name="configuration">Configuration source for secret values.</param>
public class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    /// <summary>
    /// Gets a secret by name from configuration.
    /// </summary>
    /// <param name="name">Secret key.</param>
    /// <returns>The secret value or null if not found.</returns>
    public string? GetSecret(string name)
    {
        return configuration[name];
    }
}
