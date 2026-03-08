using Muonroi.Governance.License;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Messaging.Abstractions.Events;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Wrapper-first outbox persistence built on top of MDbContext.
/// </summary>
public class MEventOutboxDbContext(
    DbContextOptions<MEventOutboxDbContext> options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null)
    : MDbContext(options, mediator, licenseGuard, logger), IEventOutboxStore
{
    public DbSet<EventOutbox> OutboxEvents => Set<EventOutbox>();

    IQueryable<EventOutbox> IEventOutboxStore.EventOutboxes => OutboxEvents.AsQueryable();

    public Task AddAsync(EventOutbox outbox, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        return OutboxEvents.AddAsync(outbox, cancellationToken).AsTask();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EventOutbox>(entity =>
        {
            entity.ToTable("EventOutbox");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventName).HasMaxLength(512);
            entity.Property(x => x.EventType).HasMaxLength(512);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.EventContent).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreationTime);
        });
    }
}
