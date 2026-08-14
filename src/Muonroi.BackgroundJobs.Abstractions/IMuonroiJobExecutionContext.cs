namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Execution context for background jobs.
/// </summary>
public interface IMuonroiJobExecutionContext : ISystemExecutionContext
{
    /// <summary>
    /// Gets the job identifier.
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Gets the job type name.
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Gets the scheduled execution time.
    /// </summary>
    DateTimeOffset ScheduledAt { get; }
}

/// <summary>
/// Default implementation of <see cref="IMuonroiJobExecutionContext"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MuonroiJobExecutionContext"/> class.
/// </remarks>
public sealed class MuonroiJobExecutionContext(
    string? tenantId,
    string? userId,
    string? username,
    string correlationId,
    string? accessToken,
    string? apiKey,
    bool isAuthenticated,
    IReadOnlyList<string>? permissions,
    string sourceType,
    string jobId,
    string jobType,
    DateTimeOffset scheduledAt) : SystemExecutionContext(tenantId, userId, username, correlationId, accessToken, apiKey, isAuthenticated, permissions, sourceType), IMuonroiJobExecutionContext
{

    /// <inheritdoc />
    public string JobId { get; } = string.IsNullOrWhiteSpace(jobId) ? Guid.NewGuid().ToString("N") : jobId;

    /// <inheritdoc />
    public string JobType { get; } = string.IsNullOrWhiteSpace(jobType) ? "unknown" : jobType;

    /// <inheritdoc />
    public DateTimeOffset ScheduledAt { get; } = scheduledAt;
}
