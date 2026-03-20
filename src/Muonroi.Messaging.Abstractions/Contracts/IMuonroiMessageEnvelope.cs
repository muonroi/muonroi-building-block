namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents the IMuonroi Message Envelope.
/// </summary>
public interface IMuonroiMessageEnvelope
{
    /// <summary>
    /// Gets the Tenant Id.
    /// </summary>
    string? TenantId { get; }
    /// <summary>
    /// Gets the User Id.
    /// </summary>
    string? UserId { get; }
    /// <summary>
    /// Gets the Username.
    /// </summary>
    string? Username { get; }
    /// <summary>
    /// Gets the Correlation Id.
    /// </summary>
    string CorrelationId { get; }
    /// <summary>
    /// Gets the Access Token.
    /// </summary>
    string? AccessToken { get; }
    /// <summary>
    /// Gets the Sent At.
    /// </summary>
    DateTimeOffset SentAt { get; }
}

/// <summary>
/// Represents the Muonroi Message Envelope.
/// </summary>
public sealed class MuonroiMessageEnvelope : IMuonroiMessageEnvelope
{
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; init; }
    /// <summary>
    /// Gets or sets the User Id.
    /// </summary>
    public string? UserId { get; init; }
    /// <summary>
    /// Gets or sets the Username.
    /// </summary>
    public string? Username { get; init; }
    /// <summary>
    /// Executes the Correlation Id operation.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// Gets or sets the Access Token.
    /// </summary>
    public string? AccessToken { get; init; }
    /// <summary>
    /// Gets or sets the Sent At.
    /// </summary>
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}
