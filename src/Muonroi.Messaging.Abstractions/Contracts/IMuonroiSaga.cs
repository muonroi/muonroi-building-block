using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents the IMuonroi Saga — a tenant-aware, auditable saga state contract.
/// </summary>
/// <remarks>
/// This contract is vendor-neutral by design: it does NOT depend on any message-bus
/// library so that <c>Muonroi.Messaging.Abstractions</c> stays free of third-party
/// coupling. The <see cref="CorrelationId"/> member mirrors the shape required by
/// message-bus saga repositories (a <see cref="Guid"/> primary key). Consumers that
/// need MassTransit saga persistence should implement
/// <c>Muonroi.Messaging.MassTransit.Contracts.IMuonroiMassTransitSaga</c>, which bridges
/// this contract to <c>MassTransit.ISaga</c> inside the adapter package.
/// </remarks>
public interface IMuonroiSaga : ITenantScoped
{
    /// <summary>
    /// Gets or sets the saga correlation identifier (primary key).
    /// </summary>
    Guid CorrelationId { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    new string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Creation Time.
    /// </summary>
    DateTime CreationTime { get; set; }
    /// <summary>
    /// Gets or sets the Last Modification Time.
    /// </summary>
    DateTime? LastModificationTime { get; set; }
}
