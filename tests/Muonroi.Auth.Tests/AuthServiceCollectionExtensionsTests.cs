using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Auth.Tests;

public sealed class AuthServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryRsaKeyStore_ShouldReplaceStores_AndRegisterJwtService()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IMDateTimeService>(new FakeDateTimeService());
        services.AddSingleton<MTokenInfo>(new MTokenInfo { SymmetricSecretKey = "test-signing-key", Issuer = "issuer", Audience = "audience" });
        services.AddSingleton<IRsaKeyStore>(new DummyRsaKeyStore());
        services.AddSingleton<ITokenRevocationStore>(new DummyTokenRevocationStore());

        services.AddInMemoryRsaKeyStore();
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IRsaKeyStore>().Should().BeOfType<InMemoryRsaKeyStore>();
        provider.GetRequiredService<ITokenRevocationStore>().Should().BeOfType<TokenRevocationStore>();
        provider.GetRequiredService<JwtService>().Should().NotBeNull();
    }

    [Fact]
    public void AddRedisRsaKeyStore_ShouldBindConfiguration_AndRegisterRedisBackedServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MTokenInfo:SigningKey"] = "configured-signing-key",
                ["MTokenInfo:Issuer"] = "issuer",
                ["MTokenInfo:Audience"] = "audience"
                ,["Auth:RsaMasterKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("12345678901234567890123456789012"))
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddSingleton<IMDateTimeService>(new FakeDateTimeService());
        services.AddSingleton<IMJsonSerializeService>(new MJsonSerializeService());
        services.AddSingleton<IDistributedCache>(new RecordingDistributedCache());

        services.AddRedisRsaKeyStore(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IRsaKeyStore>().Should().BeOfType<RedisRsaKeyStore>();
        provider.GetRequiredService<ITokenRevocationStore>().Should().BeOfType<RedisTokenRevocationStore>();
        provider.GetRequiredService<JwtService>().Should().NotBeNull();
    }

    private sealed class DummyRsaKeyStore : IRsaKeyStore
    {
        private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "kid" };

        public SigningCredentials GetCurrentSigningCredentials() => new(_key, SecurityAlgorithms.RsaSha256);
        public SecurityKey? GetKey(string kid) => string.Equals(kid, _key.KeyId, StringComparison.Ordinal) ? _key : null;
        public JsonWebKeySet GetJsonWebKeySet() => new() { Keys = { JsonWebKeyConverter.ConvertFromRSASecurityKey(_key) } };
        public IEnumerable<(string Kid, RSAParameters Key)> GetAllPublicKeys() => [];
        public void RotateKeys() { }
    }

    private sealed class DummyTokenRevocationStore : ITokenRevocationStore
    {
        public bool IsRevoked(string jti) => false;
        public void Revoke(string jti, DateTime expires) { }
    }

    private sealed class FakeDateTimeService : IMDateTimeService
    {
        public DateTime Now() => DateTime.Now;
        public DateTime UtcNow() => new(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Today() => DateTime.Today;
        public DateTime UtcToday() => UtcNow().Date;
        public double NowTs() => DateTimeOffset.Now.ToUnixTimeSeconds();
        public double UtcNowTs() => new DateTimeOffset(UtcNow()).ToUnixTimeSeconds();
    }

    private sealed class RecordingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
    }
}
