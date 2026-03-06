

namespace Muonroi.Secrets.Secrets;

public class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string? GetSecret(string name)
    {
        return configuration[name];
    }
}
