using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
using Muonroi.Governance.License;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.RuleEngine.DecisionTable.Stores.Persistence;

internal sealed class DecisionTableDbContext(
    DbContextOptions<DecisionTableDbContext> options,
    IMediator mediator,
    IOptions<DecisionTableEngineOptions> optionsAccessor,
    ILicenseGuard? licenseGuard = null,
    ILogger<MDbContext>? logger = null)
    : MDbContext(options, mediator, licenseGuard, logger)
{
    private readonly string _schema = string.IsNullOrWhiteSpace(optionsAccessor.Value.Schema) ? "dbo" : optionsAccessor.Value.Schema;

    public DbSet<DecisionTableRecordEntity> Tables => Set<DecisionTableRecordEntity>();
    public DbSet<DecisionTableVersionEntity> Versions => Set<DecisionTableVersionEntity>();
    public DbSet<DecisionTableAuditEntity> AuditEntries => Set<DecisionTableAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);

        modelBuilder.Entity<DecisionTableRecordEntity>(entity =>
        {
            entity.ToTable("DecisionTables");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.HitPolicy).HasMaxLength(64);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.ModifiedAt);
            entity.HasIndex(x => new { x.TenantId, x.IsDeleted });
            entity.HasIndex(x => new { x.HitPolicy, x.IsDeleted });
            entity.HasIndex(x => x.IsDeleted);
        });

        modelBuilder.Entity<DecisionTableVersionEntity>(entity =>
        {
            entity.ToTable("DecisionTableVersions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangeType).HasMaxLength(64);
            entity.Property(x => x.Actor).HasMaxLength(256);
            entity.Property(x => x.Reason).HasMaxLength(512);
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.TableId, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.TableId, x.Timestamp });
        });

        modelBuilder.Entity<DecisionTableAuditEntity>(entity =>
        {
            entity.ToTable("DecisionTableAuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(64);
            entity.Property(x => x.Actor).HasMaxLength(256);
            entity.Property(x => x.Reason).HasMaxLength(512);
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.TableId, x.Timestamp });
            entity.HasIndex(x => x.Timestamp);
        });

        modelBuilder.UseUtcDateTime();
    }
}
