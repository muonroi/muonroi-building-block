using Microsoft.EntityFrameworkCore;
using Muonroi.Tenancy.Core;

namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF DbContext for connector configurations and credentials.
/// Applies tenant query filters (same pattern as RuleEngineDb).
/// </summary>
public class ConnectorDbContext : DbContext
{
    /// <summary>Connector configurations.</summary>
    public DbSet<ConnectorConfigEntity> ConnectorConfigs => Set<ConnectorConfigEntity>();
    /// <summary>Connector credentials.</summary>
    public DbSet<ConnectorCredentialEntity> ConnectorCredentials => Set<ConnectorCredentialEntity>();

    /// <summary>Creates a new connector persistence context.</summary>
    /// <param name="options">DbContext options.</param>
    public ConnectorDbContext(DbContextOptions<ConnectorDbContext> options)
        : base(options) { }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConnectorConfigEntity>(entity =>
        {
            entity.ToTable("connector_configs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.TenantId).HasMaxLength(128);
            entity.Property(e => e.ConnectorType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ConfigJson).HasColumnType("jsonb");
            entity.Property(e => e.CredentialId).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(32).HasDefaultValue("active");
            entity.HasIndex(e => new { e.TenantId, e.ConnectorType });

            // Tenant query filter
            string? tenantId = TenantContext.CurrentTenantId;
            entity.HasQueryFilter(e => e.TenantId == tenantId || tenantId == null);
        });

        modelBuilder.Entity<ConnectorCredentialEntity>(entity =>
        {
            entity.ToTable("connector_credentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.TenantId).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.EncryptedValues).IsRequired();
            entity.HasIndex(e => e.TenantId);

            string? tenantId = TenantContext.CurrentTenantId;
            entity.HasQueryFilter(e => e.TenantId == tenantId || tenantId == null);
        });
    }
}
