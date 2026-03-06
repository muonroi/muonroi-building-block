namespace Muonroi.Secrets.Secrets;

public interface ISecretProvider
{
    string? GetSecret(string name);
}
