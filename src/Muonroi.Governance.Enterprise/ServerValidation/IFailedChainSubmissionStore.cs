namespace Muonroi.Governance.Enterprise.ServerValidation;

/// <summary>
/// Durable store for audit-chain submissions that failed to reach the license server
/// (network error / 5xx). Without it, a failed submission is lost and the audit trail
/// becomes unreliable. <see cref="ChainSubmitter"/> enqueues on transient failure and
/// <see cref="ChainSubmitter.RetryPendingAsync"/> drains the queue on a later attempt.
/// </summary>
public interface IFailedChainSubmissionStore
{
    /// <summary>Persists a failed submission for later retry.</summary>
    Task EnqueueAsync(PendingChainSubmission pending, CancellationToken cancellationToken = default);

    /// <summary>Lists all pending (not-yet-accepted) submissions.</summary>
    Task<IReadOnlyList<PendingChainSubmission>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates an existing pending submission (attempt count / last error).</summary>
    Task UpdateAsync(PendingChainSubmission pending, CancellationToken cancellationToken = default);

    /// <summary>Removes a pending submission once accepted by the server or dead-lettered.</summary>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// A persisted audit-chain submission awaiting retry.
/// </summary>
public sealed class PendingChainSubmission
{
    /// <summary>Stable identifier (also the on-disk file name).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The original submission request (entries + tenant).</summary>
    public ChainSubmissionRequest Request { get; set; } = new();

    /// <summary>When the submission first failed.</summary>
    public DateTimeOffset FirstFailedAtUtc { get; set; }

    /// <summary>When the most recent retry was attempted.</summary>
    public DateTimeOffset LastAttemptUtc { get; set; }

    /// <summary>Number of submission attempts made (including the first).</summary>
    public int AttemptCount { get; set; }

    /// <summary>The error from the most recent failed attempt.</summary>
    public string? LastError { get; set; }
}
