using Muonroi.Core.Abstractions.Context;

namespace Muonroi.BackgroundJobs.Abstractions;

public interface IMuonroiJobExecutionContext : ISystemExecutionContext
{
    string JobId { get; }
    string JobType { get; }
    DateTimeOffset ScheduledAt { get; }
}

public sealed class MuonroiJobExecutionContext : SystemExecutionContext, IMuonroiJobExecutionContext
{
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

    public string JobId { get; }
    public string JobType { get; }
    public DateTimeOffset ScheduledAt { get; }
}
