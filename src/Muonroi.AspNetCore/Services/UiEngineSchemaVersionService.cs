namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Service for building the UI engine schema version and calculating its hash.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public sealed class UiEngineSchemaVersionService<TDbContext>(TDbContext dbContext, IMDateTimeService dateTimeService, IMJsonSerializeService jsonSerializeService)
    where TDbContext : MDbContext
{
    /// <summary>
    /// Builds the UI engine schema version asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{MUiEngineSchemaVersion}"/> representing the built schema version.</returns>
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
