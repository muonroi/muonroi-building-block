namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Credential management for connectors. Credentials are per-tenant and encrypted at rest.
/// </summary>
public interface IConnectorCredentialStore
{
    Task<IReadOnlyDictionary<string, string>> GetAsync(string credentialId, string? tenantId, CancellationToken ct);
    Task SaveAsync(string credentialId, string? tenantId, Dictionary<string, string> values, CancellationToken ct);
    Task DeleteAsync(string credentialId, string? tenantId, CancellationToken ct);
}
