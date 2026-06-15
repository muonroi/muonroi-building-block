using Muonroi.Messaging.Abstractions.Contracts;

namespace Quickstart.Data.Events.Api.Sagas;

/// <summary>
/// A minimal saga state implementing <see cref="IMuonroiSaga"/> — the vendor-neutral,
/// tenant-aware saga contract from Muonroi.Messaging.Abstractions.
///
/// MSagaDbContext.OnModelCreating discovers every IMuonroiSaga entity, sets
/// CorrelationId as the primary key, and indexes TenantId. SaveChangesAsync stamps
/// CreationTime / LastModificationTime and auto-injects the ambient TenantId.
/// </summary>
public class OrderSaga : IMuonroiSaga
{
    /// <inheritdoc />
    public Guid CorrelationId { get; set; }

    /// <inheritdoc />
    public string? TenantId { get; set; }

    /// <inheritdoc />
    public DateTime CreationTime { get; set; }

    /// <inheritdoc />
    public DateTime? LastModificationTime { get; set; }

    /// <summary>Gets or sets the current saga state name.</summary>
    public string State { get; set; } = "Pending";

    /// <summary>Gets or sets the order total.</summary>
    public decimal Amount { get; set; }
}
