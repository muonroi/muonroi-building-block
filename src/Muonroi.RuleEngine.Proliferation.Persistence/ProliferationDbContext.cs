using Microsoft.EntityFrameworkCore;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.RuleEngine.Proliferation.Persistence.Entities;
using Muonroi.Tenancy.Core;

namespace Muonroi.RuleEngine.Proliferation.Persistence;

/// <summary>
/// Entity Framework DbContext for proliferation persistence.
/// </summary>
public class ProliferationDbContext : DbContext
{
    /// <summary>
    /// Creates a proliferation DbContext.
    /// </summary>
    /// <param name="options">DbContext options.</param>
    public ProliferationDbContext(DbContextOptions<ProliferationDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the neuron scenario entities.</summary>
    public DbSet<NeuronScenarioEntity> NeuronScenarios => Set<NeuronScenarioEntity>();

    /// <summary>Gets the scenario result entities.</summary>
    public DbSet<ScenarioResultEntity> ScenarioResults => Set<ScenarioResultEntity>();

    /// <summary>Gets the rule lineage entities.</summary>
    public DbSet<RuleLineageEntity> RuleLineages => Set<RuleLineageEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("proliferation");

        string? tenantId = TenantContext.CurrentTenantId;

        // NeuronScenario
        modelBuilder.Entity<NeuronScenarioEntity>(entity =>
        {
            entity.ToTable("neuron_scenarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.SeedRuleCode).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ScenarioName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Scope).HasConversion<int>();
            entity.Property(e => e.ParentScenarioId).HasMaxLength(64);
            entity.Property(e => e.ProliferationReason).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.InputFactsJson).HasColumnType("jsonb");
            entity.Property(e => e.ExpectedBehavior).HasMaxLength(1024);
            entity.Property(e => e.GeneratedRuleFlowGraph).HasColumnType("jsonb");
            entity.Property(e => e.Status).HasConversion<int>().HasDefaultValue(ScenarioStatus.Pending);
            entity.Property(e => e.TenantId).HasMaxLength(128);

            entity.HasIndex(e => e.SeedRuleCode);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TenantId);

            entity.HasQueryFilter(e => e.TenantId == tenantId || tenantId == null);
        });

        // ScenarioResult
        modelBuilder.Entity<ScenarioResultEntity>(entity =>
        {
            entity.ToTable("scenario_results");
            entity.HasKey(e => e.ScenarioId);
            entity.Property(e => e.ScenarioId).HasMaxLength(64);
            entity.Property(e => e.ActualBehavior).HasMaxLength(1024);
            entity.Property(e => e.OutputFactsJson).HasColumnType("jsonb");
            entity.Property(e => e.ErrorsJson).HasColumnType("jsonb").HasDefaultValue("[]");
        });

        // RuleLineage
        modelBuilder.Entity<RuleLineageEntity>(entity =>
        {
            entity.ToTable("rule_lineages");
            entity.HasKey(e => e.ScenarioId);
            entity.Property(e => e.ScenarioId).HasMaxLength(64);
            entity.Property(e => e.SeedRuleCode).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ParentScenarioId).HasMaxLength(64);
            entity.Property(e => e.Reason).HasMaxLength(1024).IsRequired();

            entity.HasIndex(e => e.SeedRuleCode);
        });
    }
}
