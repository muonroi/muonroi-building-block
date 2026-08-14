namespace Muonroi.Messaging.MassTransit.Contracts;

/// <summary>
/// Bridges the vendor-neutral <see cref="IMuonroiSaga"/> contract to
/// <see cref="global::MassTransit.ISaga"/> for consumers that use MassTransit saga
/// persistence (state machines, saga repositories).
/// </summary>
/// <remarks>
/// The MassTransit coupling lives here, in the adapter package, so that
/// <c>Muonroi.Messaging.Abstractions</c> remains free of any message-bus dependency.
/// <see cref="IMuonroiSaga.CorrelationId"/> and <see cref="global::MassTransit.ISaga.CorrelationId"/>
/// share the identical <see cref="Guid"/> signature, so a single property implementation
/// satisfies both interfaces — concrete saga state classes implement this interface and
/// gain both the Muonroi tenant/audit contract and MassTransit compatibility.
/// </remarks>
public interface IMuonroiMassTransitSaga : IMuonroiSaga, global::MassTransit.ISaga
{
}
