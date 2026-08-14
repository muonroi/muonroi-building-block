namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF-backed credential store using ASP.NET Data Protection for encryption.
/// Per-tenant key derivation via CreateProtector($"connector-creds:{tenantId}").
/// </summary>
/// <remarks>Creates a new EF-backed credential store.</remarks>
/// <param name="db">Connector database context.</param>
/// <param name="protectionProvider">Data protection provider.</param>
/// <param name="log">Optional logger.</param>
public sealed class EfConnectorCredentialStore(
    ConnectorDbContext db,
    IDataProtectionProvider protectionProvider,
    IMLog<EfConnectorCredentialStore>? log = null) : IConnectorCredentialStore
{
    private readonly ConnectorDbContext _db = db;
    private readonly IDataProtectionProvider _protectionProvider = protectionProvider;
    private readonly IMLog<EfConnectorCredentialStore>? _log = log;

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
