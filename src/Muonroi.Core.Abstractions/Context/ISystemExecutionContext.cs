namespace Muonroi.Core.Abstractions.Context;

public interface ISystemExecutionContext
{
    string? TenantId { get; }
    string? UserId { get; }
    string? Username { get; }
    string CorrelationId { get; }
    string? AccessToken { get; }
    string? ApiKey { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Permissions { get; }
    string SourceType { get; }
}

public class SystemExecutionContext : ISystemExecutionContext
{
    public static readonly SystemExecutionContext Empty = new(
        tenantId: null,
        userId: null,
        username: null,
        correlationId: Guid.NewGuid().ToString("N"),
        accessToken: null,
        apiKey: null,
        isAuthenticated: false,
        permissions: [],
        sourceType: "unknown");

    public SystemExecutionContext(
        string? tenantId,
        string? userId,
        string? username,
        string correlationId,
        string? accessToken,
        string? apiKey,
        bool isAuthenticated,
        IReadOnlyList<string>? permissions,
        string sourceType)
    {
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        Username = Normalize(username);
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim();
        AccessToken = Normalize(accessToken);
        ApiKey = Normalize(apiKey);
        IsAuthenticated = isAuthenticated;
        Permissions = permissions ?? [];
        SourceType = string.IsNullOrWhiteSpace(sourceType) ? "unknown" : sourceType.Trim().ToLowerInvariant();
    }

    public string? TenantId { get; }
    public string? UserId { get; }
    public string? Username { get; }
    public string CorrelationId { get; }
    public string? AccessToken { get; }
    public string? ApiKey { get; }
    public bool IsAuthenticated { get; }
    public IReadOnlyList<string> Permissions { get; }
    public string SourceType { get; }

    public SystemExecutionContext With(
        string? tenantId = null,
        string? userId = null,
        string? username = null,
        string? correlationId = null,
        string? accessToken = null,
        string? apiKey = null,
        bool? isAuthenticated = null,
        IReadOnlyList<string>? permissions = null,
        string? sourceType = null)
    {
        return new SystemExecutionContext(
            tenantId: tenantId ?? TenantId,
            userId: userId ?? UserId,
            username: username ?? Username,
            correlationId: correlationId ?? CorrelationId,
            accessToken: accessToken ?? AccessToken,
            apiKey: apiKey ?? ApiKey,
            isAuthenticated: isAuthenticated ?? IsAuthenticated,
            permissions: permissions ?? Permissions,
            sourceType: sourceType ?? SourceType);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
