using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Timing;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Governance.License;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Messaging.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace Muonroi.Data.EntityFrameworkCore.Saga;

public abstract class MSagaDbContext(
    DbContextOptions options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null,
    IMDateTimeService? dateTimeService = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null)
    : MDbContext(options, mediator, licenseGuard, logger, dateTimeService)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Scan for all sagas inheriting from IMuonroiSaga
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMuonroiSaga).IsAssignableFrom(entityType.ClrType))
            {
                var builder = modelBuilder.Entity(entityType.ClrType);
                builder.HasKey("CorrelationId");
                
                // Add index for TenantId for performance
                builder.HasIndex(nameof(IMuonroiSaga.TenantId));
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateSagaTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateSagaTimestamps()
    {
        var entries = ChangeTracker.Entries<IMuonroiSaga>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        DateTime now = dateTimeService?.UtcNow() ?? DateTime.UtcNow; // MBB001-exempt: fallback when IMDateTimeService not injected

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreationTime = now;
                // Auto-inject tenant id if missing
                if (string.IsNullOrWhiteSpace(entry.Entity.TenantId))
                {
                    entry.Entity.TenantId = executionContextAccessor?.Get().TenantId ?? string.Empty;
                }
            }
            entry.Entity.LastModificationTime = now;
        }
    }
}
