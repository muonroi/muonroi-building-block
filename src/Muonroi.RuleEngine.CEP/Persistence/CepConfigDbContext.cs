using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.CEP.Options;

namespace Muonroi.RuleEngine.CEP.Persistence;

internal sealed class CepConfigDbContext(
    DbContextOptions<CepConfigDbContext> options,
    IMediator mediator,
    IOptions<CepOptions> optionsAccessor,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null)
    : MDbContext(options, mediator, licenseGuard, logger)
{
    private readonly string _schema =
        string.IsNullOrWhiteSpace(optionsAccessor.Value.Schema) ? "dbo" : optionsAccessor.Value.Schema;

    public DbSet<CepConfigEntity> Configs => Set<CepConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);

        modelBuilder.Entity<CepConfigEntity>(entity =>
        {
            entity.ToTable("CepConfigs");
            entity.HasKey(x => new { x.TenantId, x.Id });
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(2048);
            entity.Property(x => x.WindowType).HasMaxLength(32);
            entity.Property(x => x.CorrelationKey).HasMaxLength(128);
            entity.Property(x => x.MetadataJson);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAtUtc });
        });

        modelBuilder.UseUtcDateTime();
    }
}
