using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents the IMuonroi Saga.
/// </summary>
public interface IMuonroiSaga : MassTransit.ISaga, ITenantScoped
{
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    new string TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Creation Time.
    /// </summary>
    DateTime CreationTime { get; set; }
    /// <summary>
    /// Gets or sets the Last Modification Time.
    /// </summary>
    DateTime? LastModificationTime { get; set; }
}
