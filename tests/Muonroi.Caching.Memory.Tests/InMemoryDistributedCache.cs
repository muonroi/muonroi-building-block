namespace Muonroi.Caching.Memory.Tests;

internal sealed class InMemoryDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = [];

    public byte[]? Get(string key)
    {
        return _store.TryGetValue(key, out byte[]? value) ? value : null;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return Task.FromResult(Get(key));
    }

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        _store.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        _ = _store.Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _store[key] = value;
    }

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
