using Muonroi.Core.Abstractions.Context;

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
public sealed class MuonroiJobExecutionContext : SystemExecutionContext, IMuonroiJobExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MuonroiJobExecutionContext"/> class.
    /// </summary>
    public MuonroiJobExecutionContext(
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
        DateTimeOffset scheduledAt)
        : base(tenantId, userId, username, correlationId, accessToken, apiKey, isAuthenticated, permissions, sourceType)
    {
        JobId = string.IsNullOrWhiteSpace(jobId) ? Guid.NewGuid().ToString("N") : jobId;
        JobType = string.IsNullOrWhiteSpace(jobType) ? "unknown" : jobType;
        ScheduledAt = scheduledAt;
    }

    /// <inheritdoc />
    public string JobId { get; }

    /// <inheritdoc />
    public string JobType { get; }

    /// <inheritdoc />
    public DateTimeOffset ScheduledAt { get; }
}
