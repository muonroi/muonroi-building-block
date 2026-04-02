using Muonroi.Governance.License;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Messaging.Abstractions.Events;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Wrapper-first outbox persistence built on top of MDbContext.
/// </summary>
/// <param name="options">The DbContext options.</param>
/// <param name="mediator">The mediator for domain events.</param>
/// <param name="licenseGuard">Optional license guard.</param>
/// <param name="logger">Optional logger.</param>
public class MEventOutboxDbContext(
    DbContextOptions<MEventOutboxDbContext> options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null)
    : MDbContext(options, mediator, licenseGuard, logger), IEventOutboxStore
{
    /// <summary>
    /// Gets the outbox events set.
    /// </summary>
    public DbSet<EventOutbox> OutboxEvents => Set<EventOutbox>();

    /// <summary>
    /// Gets the message inbox set.
    /// </summary>
    public DbSet<MessageInbox> MessageInbox => Set<MessageInbox>();

    IQueryable<EventOutbox> IEventOutboxStore.EventOutboxes => OutboxEvents.AsQueryable();

    /// <summary>
    /// Adds an outbox entry to the store.
    /// </summary>
    /// <param name="outbox">The outbox entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AddAsync(EventOutbox outbox, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        return OutboxEvents.AddAsync(outbox, cancellationToken).AsTask();
    }

    /// <summary>
    /// Configures the outbox schema mapping.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
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

        modelBuilder.Entity<MessageInbox>(entity =>
        {
            entity.ToTable("MessageInbox");
            entity.HasKey(x => x.MessageId);
            entity.Property(x => x.ConsumerName).HasMaxLength(256);
        });
    }
}
