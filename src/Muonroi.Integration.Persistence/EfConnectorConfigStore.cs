namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF-backed connector config store.
/// </summary>
/// <remarks>Creates a new EF-backed connector config store.</remarks>
/// <param name="db">Connector database context.</param>
public sealed class EfConnectorConfigStore(ConnectorDbContext db) : IConnectorConfigStore
{
    private readonly ConnectorDbContext _db = db;

    /// <inheritdoc />
    public async Task<ConnectorConfigDto?> GetByIdAsync(string id, string? tenantId, CancellationToken ct)
    {
        ConnectorConfigEntity? entity = await _db.ConnectorConfigs
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConnectorConfigDto>> ListAsync(string? tenantId, string? ownerId, CancellationToken ct)
    {
        IQueryable<ConnectorConfigEntity> q = _db.ConnectorConfigs;
        if (ownerId is not null)
        {
            q = q.Where(e => e.OwnerId == ownerId);
        }
        List<ConnectorConfigEntity> entities = await q
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);
        return entities.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ConnectorConfigDto> SaveAsync(ConnectorConfigDto config, CancellationToken ct)
    {
        ConnectorConfigEntity? existing = string.IsNullOrEmpty(config.Id)
            ? null
            : await _db.ConnectorConfigs.FirstOrDefaultAsync(e => e.Id == config.Id, ct);

        if (existing is not null)
        {
            existing.Name = config.Name;
            existing.ConnectorType = config.ConnectorType;
            existing.ConfigJson = config.ConfigJson;
            existing.CredentialId = config.CredentialId;
            existing.Status = config.Status;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new ConnectorConfigEntity
            {
                Id = string.IsNullOrEmpty(config.Id) ? Guid.NewGuid().ToString("N") : config.Id,
                TenantId = config.TenantId ?? TenantContext.CurrentTenantId,
                ConnectorType = config.ConnectorType,
                Name = config.Name,
                ConfigJson = config.ConfigJson,
                CredentialId = config.CredentialId,
                Status = config.Status,
                OwnerId = config.OwnerId,
            };
            _db.ConnectorConfigs.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, string? tenantId, CancellationToken ct)
    {
        ConnectorConfigEntity? entity = await _db.ConnectorConfigs
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is not null)
        {
            _db.ConnectorConfigs.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static ConnectorConfigDto ToDto(ConnectorConfigEntity entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        ConnectorType = entity.ConnectorType,
        Name = entity.Name,
        ConfigJson = entity.ConfigJson,
        CredentialId = entity.CredentialId,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        OwnerId = entity.OwnerId,
    };
}
