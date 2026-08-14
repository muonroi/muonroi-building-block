namespace Muonroi.RuleEngine.CEP.Repositories;

/// <summary>
/// EF Core-backed CEP configuration store for production deployments.
/// </summary>
internal sealed class EfCoreCepConfigRepository(
    CepConfigDbContext dbContext,
    IMDateTimeService dateTimeService,
    IMJsonSerializeService jsonSerializeService,
    ISystemExecutionContextAccessor contextAccessor) : ICepConfigRepository
{
    public async Task<IReadOnlyList<CepConfig>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = ResolveTenantId();
        CepMetrics.ConfigReads.Add(1, CreateTags("list", tenantId));

        List<CepConfigEntity> entities = await dbContext.Configs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(Map)];
    }

    public async Task<CepConfig?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = ResolveTenantId();
        CepMetrics.ConfigReads.Add(1, CreateTags("get", tenantId));

        CepConfigEntity? entity = await dbContext.Configs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == NormalizeRequired(id, nameof(id)), cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<CepConfig> SaveAsync(CepConfig config, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = NormalizeTenantId(config.TenantId);
        if (string.Equals(tenantId, "_global", StringComparison.OrdinalIgnoreCase))
        {
            tenantId = ResolveTenantId();
        }

        DateTime now = dateTimeService.UtcNow();
        CepMetrics.ConfigWrites.Add(1, CreateTags("save", tenantId));

        string configId = NormalizeRequired(config.Id, nameof(config.Id));
        CepConfigEntity? entity = await dbContext.Configs
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == configId, cancellationToken);

        DateTime createdAtUtc = entity?.CreatedAtUtc ?? now;
        CepConfig prepared = Prepare(config, tenantId, now, createdAtUtc);

        if (entity is null)
        {
            entity = new CepConfigEntity
            {
                TenantId = prepared.TenantId,
                Id = prepared.Id
            };
            dbContext.Configs.Add(entity);
        }

        entity.Name = prepared.Name;
        entity.Description = prepared.Description;
        entity.WindowType = prepared.WindowType.ToString();
        entity.WindowSizeSeconds = (int)Math.Round(prepared.WindowSize.TotalSeconds);
        entity.TimeToLiveSeconds = (int)Math.Round(prepared.TimeToLive.TotalSeconds);
        entity.CorrelationKey = prepared.CorrelationKey;
        entity.MetadataJson = jsonSerializeService.Serialize(prepared.Metadata);
        entity.CreatedAtUtc = prepared.CreatedAtUtc;
        entity.UpdatedAtUtc = prepared.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return prepared with
        {
            Metadata = new Dictionary<string, string>(prepared.Metadata, StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = ResolveTenantId();
        string configId = NormalizeRequired(id, nameof(id));
        CepMetrics.ConfigWrites.Add(1, CreateTags("delete", tenantId));

        CepConfigEntity? entity = await dbContext.Configs
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == configId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.Configs.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private CepConfig Map(CepConfigEntity entity)
    {
        IReadOnlyDictionary<string, string> metadata =
            jsonSerializeService.Deserialize<Dictionary<string, string>>(entity.MetadataJson) ??
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new CepConfig
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Description = entity.Description,
            WindowType = Enum.TryParse(entity.WindowType, true, out WindowType windowType) ? windowType : WindowType.Sliding,
            WindowSize = TimeSpan.FromSeconds(entity.WindowSizeSeconds),
            TimeToLive = TimeSpan.FromSeconds(entity.TimeToLiveSeconds),
            CorrelationKey = entity.CorrelationKey,
            Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = EnsureUtc(entity.CreatedAtUtc),
            UpdatedAtUtc = EnsureUtc(entity.UpdatedAtUtc)
        };
    }

    private CepConfig Prepare(CepConfig config, string tenantId, DateTime updatedAtUtc, DateTime createdAtUtc)
    {
        return new CepConfig
        {
            Id = NormalizeRequired(config.Id, nameof(config.Id)),
            TenantId = tenantId,
            Name = NormalizeRequired(config.Name, nameof(config.Name)),
            Description = NormalizeOptional(config.Description),
            WindowType = config.WindowType,
            WindowSize = config.WindowSize,
            TimeToLive = config.TimeToLive,
            CorrelationKey = NormalizeOptional(config.CorrelationKey) ?? "default",
            Metadata = CloneMetadata(config.Metadata),
            CreatedAtUtc = EnsureUtc(createdAtUtc),
            UpdatedAtUtc = EnsureUtc(updatedAtUtc)
        };
    }

    private string ResolveTenantId()
    {
        return NormalizeTenantId(contextAccessor.Get().TenantId);
    }

    private static Dictionary<string, string> CloneMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        return new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    private static TagList CreateTags(string operation, string tenantId)
    {
        return new TagList
        {
            { "cep.operation", operation },
            { "tenant.id", tenantId }
        };
    }

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim();
    }

    private static string NormalizeRequired(string? value, string paramName)
    {
        return MGuard.NotEmpty(value, paramName).Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
