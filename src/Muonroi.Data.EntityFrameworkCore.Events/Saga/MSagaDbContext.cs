using Muonroi.Core.Abstractions.Context;
using Muonroi.Messaging.Abstractions.Contracts;

namespace Muonroi.Data.EntityFrameworkCore.Saga;

/// <summary>
/// Base saga DbContext that tracks saga entities and timestamps.
/// </summary>
/// <param name="options">The DbContext options.</param>
/// <param name="mediator">The mediator for domain events.</param>
/// <param name="licenseGuard">Optional license guard.</param>
/// <param name="logger">Optional logger.</param>
/// <param name="dateTimeService">Optional date/time service.</param>
/// <param name="executionContextAccessor">Optional execution context accessor.</param>
public abstract class MSagaDbContext(
    DbContextOptions options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null,
    IMDateTimeService? dateTimeService = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null)
    : MDbContext(options, mediator, licenseGuard, logger, dateTimeService)
{
    private readonly IMDateTimeService? _dateTimeService = dateTimeService;
    private readonly ISystemExecutionContextAccessor? _executionContextAccessor = executionContextAccessor;

    /// <summary>
    /// Configures saga entity mappings.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (EntityTypeBuilder? builder in
        // Scan for all sagas inheriting from IMuonroiSaga
        from IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes()
        where typeof(IMuonroiSaga).IsAssignableFrom(entityType.ClrType)
        let builder = modelBuilder.Entity(entityType.ClrType)
        select builder)
        {
            builder.HasKey("CorrelationId");
            // Add index for TenantId for performance
            builder.HasIndex(nameof(IMuonroiSaga.TenantId));
        }
    }

    /// <summary>
    /// Saves changes and updates saga timestamps before persisting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateSagaTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateSagaTimestamps()
    {
        IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMuonroiSaga>> entries = ChangeTracker.Entries<IMuonroiSaga>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        DateTime now = _dateTimeService?.UtcNow() ?? DateTime.UtcNow; // MBB001-exempt: fallback when IMDateTimeService not injected

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMuonroiSaga>? entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreationTime = now;
                // Auto-inject tenant id if missing
                if (string.IsNullOrWhiteSpace(entry.Entity.TenantId))
                {
                    entry.Entity.TenantId = _executionContextAccessor?.Get().TenantId ?? string.Empty;
                }
            }
            entry.Entity.LastModificationTime = now;
        }
    }
}
