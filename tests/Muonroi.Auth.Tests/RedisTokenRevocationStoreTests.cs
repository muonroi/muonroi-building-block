namespace Muonroi.Auth.Tests;

public sealed class RedisTokenRevocationStoreTests
{
    [Fact]
    public void Revoke_WhenJtiValidAndNotExpired_ShouldWriteToCache()
    {
        RecordingDistributedCache cache = new();
        FixedDateTimeService dateTimeService = new();
        RedisTokenRevocationStore store = new(cache, dateTimeService);
        DateTime expires = dateTimeService.UtcNow().AddMinutes(10);

        store.Revoke("token-1", expires);

        cache.LastKey.Should().Be("revoked:token-1");
        cache.LastValue.Should().Be("1");
        cache.LastOptions.Should().NotBeNull();
        cache.LastOptions!.AbsoluteExpiration.Should().Be(expires);
    }

    [Fact]
    public void Revoke_WhenJtiMissingOrExpired_ShouldDoNothing()
    {
        RecordingDistributedCache cache = new();
        FixedDateTimeService dateTimeService = new();
        RedisTokenRevocationStore store = new(cache, dateTimeService);

        store.Revoke("", dateTimeService.UtcNow().AddMinutes(10));
        store.Revoke("token-2", dateTimeService.UtcNow());

        cache.SetCallCount.Should().Be(0);
    }

    [Fact]
    public void IsRevoked_ShouldReturnTrueOnlyWhenCacheContainsEntry()
    {
        RecordingDistributedCache cache = new();
        cache.Stored["revoked:token-1"] = Encoding.UTF8.GetBytes("1");
        RedisTokenRevocationStore store = new(cache, new FixedDateTimeService());

        store.IsRevoked("").Should().BeFalse();
        store.IsRevoked("missing").Should().BeFalse();
        store.IsRevoked("token-1").Should().BeTrue();
    }

    private sealed class FixedDateTimeService : IMDateTimeService
    {
        private static readonly DateTime Utc = new(2026, 3, 23, 12, 0, 0, DateTimeKind.Utc);

        public DateTime Now() => Utc.ToLocalTime();
        public DateTime UtcNow() => Utc;
        public DateTime Today() => Now().Date;
        public DateTime UtcToday() => Utc.Date;
        public double NowTs() => new DateTimeOffset(Now()).ToUnixTimeSeconds();
        public double UtcNowTs() => new DateTimeOffset(Utc).ToUnixTimeSeconds();
    }

    private sealed class RecordingDistributedCache : IDistributedCache
    {
        public Dictionary<string, byte[]> Stored { get; } = new(StringComparer.Ordinal);
        public string? LastKey { get; private set; }
        public string? LastValue { get; private set; }
        public DistributedCacheEntryOptions? LastOptions { get; private set; }
        public int SetCallCount { get; private set; }

        public byte[]? Get(string key) => Stored.TryGetValue(key, out byte[]? value) ? value : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => Stored.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetCallCount++;
            LastKey = key;
            LastValue = Encoding.UTF8.GetString(value);
            LastOptions = options;
            Stored[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
