using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.UiEngine.Catalog.Options;

namespace Muonroi.UiEngine.Catalog.Persistence;

internal sealed class UiEngineCatalogDbContext(
    DbContextOptions<UiEngineCatalogDbContext> options,
    IMediator mediator,
    IOptions<UiEngineCatalogOptions> optionsAccessor,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null)
    : MDbContext(options, mediator, licenseGuard, logger)
{
    private readonly string _schema =
        string.IsNullOrWhiteSpace(optionsAccessor.Value.Schema) ? "dbo" : optionsAccessor.Value.Schema;

    public DbSet<CatalogSnapshotEntity> Snapshots => Set<CatalogSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);

        modelBuilder.Entity<CatalogSnapshotEntity>(entity =>
        {
            entity.ToTable("UiEngineCatalogSnapshots");
            entity.HasKey(x => x.SnapshotId);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.GraphJson);
            entity.HasIndex(x => new { x.TenantId, x.CapturedAt });
            entity.HasIndex(x => x.CapturedAt);
        });

        modelBuilder.UseUtcDateTime();
    }
}
