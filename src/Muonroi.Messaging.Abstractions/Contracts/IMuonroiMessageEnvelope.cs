namespace Muonroi.Messaging.Abstractions.Contracts;

public interface IMuonroiMessageEnvelope
{
    string? TenantId { get; }
    string? UserId { get; }
    string? Username { get; }
    string CorrelationId { get; }
    string? AccessToken { get; }
    DateTimeOffset SentAt { get; }
}

public sealed class MuonroiMessageEnvelope : IMuonroiMessageEnvelope
{
    public string? TenantId { get; init; }
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public string? AccessToken { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}
