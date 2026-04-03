namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Credential management for connectors. Credentials are per-tenant and encrypted at rest.
/// </summary>
public interface IConnectorCredentialStore
{
    /// <summary>Gets decrypted credential values by id.</summary>
    /// <param name="credentialId">Credential identifier.</param>
    /// <param name="tenantId">Tenant identifier, or null for global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, string>> GetAsync(string credentialId, string? tenantId, CancellationToken ct);
    /// <summary>Saves credential values for a connector.</summary>
    /// <param name="credentialId">Credential identifier.</param>
    /// <param name="tenantId">Tenant identifier, or null for global.</param>
    /// <param name="values">Key-value pairs to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(string credentialId, string? tenantId, Dictionary<string, string> values, CancellationToken ct);
    /// <summary>Deletes stored credentials by id.</summary>
    /// <param name="credentialId">Credential identifier.</param>
    /// <param name="tenantId">Tenant identifier, or null for global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string credentialId, string? tenantId, CancellationToken ct);
}
