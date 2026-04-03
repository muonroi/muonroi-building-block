namespace Muonroi.Bff;

/// <summary>
/// Simple in-memory implementation of <see cref="ITokenStore"/> suitable for testing
/// or single-instance deployments. Tokens are never persisted to the client.
/// </summary>
public class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, string> _refreshTokens = new();

    /// <inheritdoc />
    public Task StoreRefreshTokenAsync(string subject, string refreshToken)
    {
        _refreshTokens[subject] = refreshToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetRefreshTokenAsync(string subject)
    {
        _refreshTokens.TryGetValue(subject, out string? token);
        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task RemoveRefreshTokenAsync(string subject)
    {
        _refreshTokens.TryRemove(subject, out _);
        return Task.CompletedTask;
    }
}
