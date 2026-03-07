using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Messaging.Abstractions.Contracts;

public interface IMuonroiSaga : global::MassTransit.ISaga, ITenantScoped
{
    new string TenantId { get; set; }
    DateTime CreationTime { get; set; }
    DateTime? LastModificationTime { get; set; }
}
