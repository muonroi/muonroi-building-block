using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Muonroi.Tenancy.Core;
using Muonroi.Integration.Abstractions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF-backed credential store using ASP.NET Data Protection for encryption.
/// Per-tenant key derivation via CreateProtector($"connector-creds:{tenantId}").
/// </summary>
public sealed class EfConnectorCredentialStore : IConnectorCredentialStore
{
    private readonly ConnectorDbContext _db;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly IMLog<EfConnectorCredentialStore>? _log;

    /// <summary>Creates a new EF-backed credential store.</summary>
    /// <param name="db">Connector database context.</param>
    /// <param name="protectionProvider">Data protection provider.</param>
    /// <param name="log">Optional logger.</param>
    public EfConnectorCredentialStore(
        ConnectorDbContext db,
        IDataProtectionProvider protectionProvider,
        IMLog<EfConnectorCredentialStore>? log = null)
    {
        _db = db;
        _protectionProvider = protectionProvider;
        _log = log;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(string credentialId, string? tenantId, CancellationToken ct)
    {
        ConnectorCredentialEntity? entity = await _db.ConnectorCredentials
            .FirstOrDefaultAsync(e => e.Id == credentialId, ct);

        if (entity is null)
        {
            return new Dictionary<string, string>();
        }

        IDataProtector protector = _protectionProvider.CreateProtector($"connector-creds:{tenantId ?? "global"}");
        string decrypted = protector.Unprotect(entity.EncryptedValues);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted) ?? new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public async Task SaveAsync(string credentialId, string? tenantId, Dictionary<string, string> values, CancellationToken ct)
    {
        IDataProtector protector = _protectionProvider.CreateProtector($"connector-creds:{tenantId ?? "global"}");
        string encrypted = protector.Protect(JsonSerializer.Serialize(values));

        ConnectorCredentialEntity? existing = await _db.ConnectorCredentials
            .FirstOrDefaultAsync(e => e.Id == credentialId, ct);

        if (existing is not null)
        {
            existing.EncryptedValues = encrypted;
            existing.Name = credentialId;
        }
        else
        {
            _db.ConnectorCredentials.Add(new ConnectorCredentialEntity
            {
                Id = credentialId,
                TenantId = tenantId ?? TenantContext.CurrentTenantId,
                Name = credentialId,
                EncryptedValues = encrypted
            });
        }

        await _db.SaveChangesAsync(ct);
        _log?.Info("Saved credentials '{CredentialId}' for tenant '{TenantId}'.", credentialId, tenantId);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string credentialId, string? tenantId, CancellationToken ct)
    {
        ConnectorCredentialEntity? entity = await _db.ConnectorCredentials
            .FirstOrDefaultAsync(e => e.Id == credentialId, ct);

        if (entity is not null)
        {
            _db.ConnectorCredentials.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
