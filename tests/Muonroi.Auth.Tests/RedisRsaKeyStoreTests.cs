namespace Muonroi.Auth.Tests;

using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Reflection;

public class RedisRsaKeyStoreTests
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly TestRsaKeyStore _store;
    private readonly byte[] _masterKey = new byte[32];

    private class ManualJsonService : IMJsonSerializeService
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        public string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, Options);
        public T? Deserialize<T>(string text) => JsonSerializer.Deserialize<T>(text, Options);
    }

    private class TestRsaKeyStore(IDistributedCache cache, IConfiguration configuration, IMJsonSerializeService jsonService)
        : RedisRsaKeyStore(cache, configuration, jsonService)
    {
        public SecurityKey? DummyKey { get; set; }

        protected override string EncryptPrivateParameters(RSAParameters parameters)
        {
            return "dummy-encrypted";
        }

        protected override RSAParameters DecryptPrivateParameters(string encodedPayload)
        {
            return new RSAParameters();
        }

        public override SecurityKey? GetKey(string kid)
        {
            if (string.IsNullOrWhiteSpace(kid)) return null;
            return DummyKey;
        }
    }

    public RedisRsaKeyStoreTests()
    {
        // Fixed 32-byte master key for AesGcm
        _masterKey = new byte[32];
        for (int i = 0; i < 32; i++) _masterKey[i] = (byte)i;
        var masterKeyBase64 = Convert.ToBase64String(_masterKey);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RsaMasterKey"] = masterKeyBase64
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        var serviceProvider = services.BuildServiceProvider();
        _cache = serviceProvider.GetRequiredService<IDistributedCache>();
        var jsonService = new ManualJsonService();

        _store = new TestRsaKeyStore(_cache, _configuration, jsonService);
        
        using var rsa = RSA.Create();
        _store.DummyKey = new RsaSecurityKey(rsa.ExportParameters(true));
        
        _store.RotateKeys();
    }

    [Fact]
    public void RotateKeys_ShouldSetCacheKeys()
    {
        // Act
        _store.RotateKeys();

        // Assert
        _cache.GetString("rsakey:current").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetKey_ShouldReturnNull_WhenKidIsEmpty()
    {
        // Act
        var result = _store.GetKey("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetJsonWebKeySet_ShouldReturnKeys()
    {
        // Arrange
        _store.RotateKeys();

        // Act
        var jwks = _store.GetJsonWebKeySet();

        // Assert
        jwks.Keys.Should().NotBeEmpty();
    }

    [Fact]
    public void GetCurrentSigningCredentials_ShouldWork()
    {
        // Act
        var credentials = _store.GetCurrentSigningCredentials();

        // Assert
        credentials.Should().NotBeNull();
    }
}
