namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// Defines the execution context of the system, including information about the tenant, user, and correlation identifier.
/// </summary>
public interface ISystemExecutionContext
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Gets the correlation identifier for tracking requests through the system.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the access token associated with the current context. This token can be forwarded by downstream transports such as gRPC clients.
    /// </summary>
    string? AccessToken { get; }

    /// <summary>
    /// Gets the API key associated with the current context.
    /// </summary>
    string? ApiKey { get; }

    /// <summary>
    /// Gets a value indicating whether the current execution is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets a list of permissions granted to the current user in this context.
    /// </summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Gets the type of the source that initiated the execution.
    /// </summary>
    string SourceType { get; }
}

/// <summary>
/// A default implementation of <see cref="ISystemExecutionContext"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SystemExecutionContext"/> class.
/// </remarks>
/// <param name="tenantId">The tenant identifier.</param>
/// <param name="userId">The user identifier.</param>
/// <param name="username">The username.</param>
/// <param name="correlationId">The correlation identifier.</param>
/// <param name="accessToken">The access token.</param>
/// <param name="apiKey">The API key.</param>
/// <param name="isAuthenticated">Whether the context is authenticated.</param>
/// <param name="permissions">The list of permissions.</param>
/// <param name="sourceType">The type of source.</param>
public class SystemExecutionContext(
    string? tenantId,
    string? userId,
    string? username,
    string correlationId,
    string? accessToken,
    string? apiKey,
    bool isAuthenticated,
    IReadOnlyList<string>? permissions,
    string sourceType) : ISystemExecutionContext
{
    /// <summary>
    /// An empty execution context instance.
    /// </summary>
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

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public string? TenantId { get; } = Normalize(tenantId);

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string? UserId { get; } = Normalize(userId);

    /// <summary>
    /// Gets the username.
    /// </summary>
    public string? Username { get; } = Normalize(username);

    /// <summary>
    /// Gets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; } = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim();

    /// <summary>
    /// Gets the access token. This token can be forwarded by downstream transports such as gRPC clients.
    /// </summary>
    public string? AccessToken { get; } = Normalize(accessToken);

    /// <summary>
    /// Gets the API key.
    /// </summary>
    public string? ApiKey { get; } = Normalize(apiKey);

    /// <summary>
    /// Gets a value indicating whether the current execution is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; } = isAuthenticated;

    /// <summary>
    /// Gets the list of permissions.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; } = permissions ?? [];

    /// <summary>
    /// Gets the source type.
    /// </summary>
    public string SourceType { get; } = string.IsNullOrWhiteSpace(sourceType) ? "unknown" : sourceType.Trim().ToLowerInvariant();

    /// <summary>
    /// Creates a new execution context by copying the current context and updating specific fields.
    /// </summary>
    /// <param name="tenantId">The new tenant identifier.</param>
    /// <param name="userId">The new user identifier.</param>
    /// <param name="username">The new username.</param>
    /// <param name="correlationId">The new correlation identifier.</param>
    /// <param name="accessToken">The new access token.</param>
    /// <param name="apiKey">The new API key.</param>
    /// <param name="isAuthenticated">The new authentication status.</param>
    /// <param name="permissions">The new permissions list.</param>
    /// <param name="sourceType">The new source type.</param>
    /// <returns>A new <see cref="SystemExecutionContext"/> instance.</returns>
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
