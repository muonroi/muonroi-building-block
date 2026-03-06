namespace Muonroi.AspNetCore.Services;

public sealed class UiEngineSchemaVersionService<TDbContext>(TDbContext dbContext, IMDateTimeService dateTimeService, IMJsonSerializeService jsonSerializeService)
    where TDbContext : MDbContext
{
    public async Task<MUiEngineSchemaVersion> BuildUiEngineSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        var fingerprint = await dbContext.Set<MPermission>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.UiKey)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.UiKey,
                x.Name,
                x.Type,
                x.ParentUiKey,
                x.IsGranted,
                x.Order
            })
            .ToListAsync(cancellationToken);

        string payload = jsonSerializeService.Serialize(new
        {
            runtimeSchemaVersion = MUiEngineManifest.MSchemaVersionV2,
            permissions = fingerprint
        });

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return new MUiEngineSchemaVersion
        {
            Version = MUiEngineManifest.MSchemaVersionV2,
            SchemaHash = hash,
            OpenApiHash = hash,
            GeneratedAtUtc = dateTimeService.UtcNow()
        };
    }
}
