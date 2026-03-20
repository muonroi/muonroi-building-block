using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace Muonroi.BuildingBlock.IntegrationTests;

/// <summary>
/// Shared process-wide distributed cache used by integration tests to simulate
/// cross-instance cache coherence without external Redis.
/// </summary>
internal sealed class SharedInMemoryDistributedCache : IDistributedCache
{
    private sealed record Entry(
        byte[] Value,
        DateTimeOffset? AbsoluteExpiration,
        TimeSpan? SlidingExpiration,
        DateTimeOffset LastAccessUtc);

    private static readonly ConcurrentDictionary<string, Entry> Store = new(StringComparer.Ordinal);

    public byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!TryGetValidEntry(key, out Entry entry))
        {
            return null;
        }

        if (entry.SlidingExpiration.HasValue)
        {
            Entry refreshed = entry with { LastAccessUtc = DateTimeOffset.UtcNow };
            Store[key] = refreshed;
        }

        return [.. entry.Value];
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return await Task.FromResult(Get(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? absoluteExpiration = ResolveAbsoluteExpiration(now, options);
        Entry entry = new(
            [.. value],
            absoluteExpiration,
            options.SlidingExpiration,
            now);

        Store[key] = entry;
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Set(key, value, options);
        await Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!TryGetValidEntry(key, out Entry entry))
        {
            return;
        }

        if (!entry.SlidingExpiration.HasValue)
        {
            return;
        }

        Store[key] = entry with { LastAccessUtc = DateTimeOffset.UtcNow };
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Refresh(key);
        await Task.CompletedTask;
    }

    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _ = Store.TryRemove(key, out _);
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Remove(key);
        await Task.CompletedTask;
    }

    private static bool TryGetValidEntry(string key, out Entry entry)
    {
        if (!Store.TryGetValue(key, out Entry? existing) || existing is null)
        {
            entry = default!;
            return false;
        }

        entry = existing;

        if (!IsExpired(entry))
        {
            return true;
        }

        _ = Store.TryRemove(key, out _);
        entry = default!;
        return false;
    }

    private static bool IsExpired(Entry entry)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (entry.AbsoluteExpiration.HasValue && now >= entry.AbsoluteExpiration.Value)
        {
            return true;
        }

        if (entry.SlidingExpiration.HasValue && now - entry.LastAccessUtc >= entry.SlidingExpiration.Value)
        {
            return true;
        }

        return false;
    }

    private static DateTimeOffset? ResolveAbsoluteExpiration(
        DateTimeOffset now,
        DistributedCacheEntryOptions options)
    {
        DateTimeOffset? absolute = options.AbsoluteExpiration;
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            DateTimeOffset relativeAbsolute = now.Add(options.AbsoluteExpirationRelativeToNow.Value);
            absolute = absolute.HasValue
                ? (absolute.Value <= relativeAbsolute ? absolute : relativeAbsolute)
                : relativeAbsolute;
        }

        return absolute;
    }
}
