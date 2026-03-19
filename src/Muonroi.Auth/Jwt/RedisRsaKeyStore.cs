using Microsoft.Extensions.Caching.Distributed;

namespace Muonroi.Auth.Jwt;

/// <summary>
/// A Redis-backed implementation of the IRsaKeyStore.
/// </summary>
public sealed class RedisRsaKeyStore : IRsaKeyStore
{
    private const string CurrentKidKey = "rsakey:current";
    private const string KeyIndexKey = "rsakey:index";
    private static readonly TimeSpan KeyTtl = TimeSpan.FromDays(30);

    private readonly IDistributedCache _cache;
    private readonly IMJsonSerializeService _jsonService;
    private readonly byte[] _masterKey;
    private readonly SemaphoreSlim _rotationLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRsaKeyStore"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache for storage.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="jsonService">The JSON serialization service.</param>
    public RedisRsaKeyStore(IDistributedCache cache, IConfiguration configuration, IMJsonSerializeService jsonService)
    {
        _cache = cache;
        _jsonService = jsonService;
        _masterKey = ReadMasterKey(configuration);
        EnsureInitialized();
    }

    /// <summary>
    /// Gets the current signing credentials from Redis.
    /// </summary>
    /// <returns>The current signing credentials.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the key cannot be resolved.</exception>
    public SigningCredentials GetCurrentSigningCredentials()
    {
        string? kid = _cache.GetString(CurrentKidKey);
        if (string.IsNullOrWhiteSpace(kid))
        {
            RotateKeys();
            kid = _cache.GetString(CurrentKidKey);
        }

        if (string.IsNullOrWhiteSpace(kid))
        {
            throw new InvalidOperationException("Unable to resolve current RSA key id.");
        }

        SecurityKey? key = GetKey(kid);
        if (key is not RsaSecurityKey rsaKey)
        {
            RotateKeys();
            key = GetKey(_cache.GetString(CurrentKidKey)!);
            if (key is not RsaSecurityKey fallbackKey)
            {
                throw new InvalidOperationException("Unable to resolve current RSA signing key.");
            }

            rsaKey = fallbackKey;
        }

        return new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// Rotates the RSA keys by creating a new key and updating the index in Redis.
    /// </summary>
    public void RotateKeys()
    {
        _rotationLock.Wait();
        try
        {
            using RSA rsa = RSA.Create(2048);
            RSAParameters privateParameters = rsa.ExportParameters(true);
            RSAParameters publicParameters = rsa.ExportParameters(false);
            string kid = Guid.NewGuid().ToString("N");

            _cache.SetString(GetPrivateKeyName(kid), EncryptPrivateParameters(privateParameters), CreateTtlOptions());
            _cache.SetString(GetPublicKeyName(kid), _jsonService.Serialize(publicParameters), CreateTtlOptions());
            _cache.SetString(CurrentKidKey, kid, CreateTtlOptions());

            List<string> previousKeyOrder = LoadKeyOrder(includeRaw: true);
            List<string> keyOrder = [.. previousKeyOrder];
            keyOrder.Insert(0, kid);
            keyOrder = [.. keyOrder.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(2)];
            SaveKeyOrder(keyOrder);

            List<string> removeKids = [.. previousKeyOrder.Except(keyOrder, StringComparer.Ordinal)];
            foreach (string oldKid in removeKids)
            {
                _cache.Remove(GetPrivateKeyName(oldKid));
                _cache.Remove(GetPublicKeyName(oldKid));
            }
        }
        finally
        {
            _rotationLock.Release();
        }
    }

    /// <summary>
    /// Retrieves a specific security key by its key identifier from Redis.
    /// </summary>
    /// <param name="kid">The unique key identifier.</param>
    /// <returns>The security key if found; otherwise, null.</returns>
    public SecurityKey? GetKey(string kid)
    {
        if (string.IsNullOrWhiteSpace(kid))
        {
            return null;
        }

        string? encryptedPrivate = _cache.GetString(GetPrivateKeyName(kid));
        if (string.IsNullOrWhiteSpace(encryptedPrivate))
        {
            return null;
        }

        RSAParameters parameters = DecryptPrivateParameters(encryptedPrivate);
        RSA rsa = RSA.Create();
        rsa.ImportParameters(parameters);
        return new RsaSecurityKey(rsa)
        {
            KeyId = kid
        };
    }

    /// <summary>
    /// Gets the current JSON Web Key Set (JWKS) from the public keys stored in Redis.
    /// </summary>
    /// <returns>The set of JSON Web Keys.</returns>
    public JsonWebKeySet GetJsonWebKeySet()
    {
        List<string> kids = LoadKeyOrder();
        JsonWebKeySet jsonWebKeySet = new();

        foreach (string kid in kids)
        {
            string? serializedPublic = _cache.GetString(GetPublicKeyName(kid));
            if (string.IsNullOrWhiteSpace(serializedPublic))
            {
                continue;
            }

            RSAParameters parameters = _jsonService.Deserialize<RSAParameters>(serializedPublic)!;
            using RSA rsa = RSA.Create();
            rsa.ImportParameters(parameters);

            RsaSecurityKey securityKey = new(rsa)
            {
                KeyId = kid
            };
            JsonWebKey jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
            jwk.Kid = kid;
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            jsonWebKeySet.Keys.Add(jwk);
        }

        return jsonWebKeySet;
    }

    private void EnsureInitialized()
    {
        string? currentKid = _cache.GetString(CurrentKidKey);
        if (string.IsNullOrWhiteSpace(currentKid))
        {
            RotateKeys();
        }
    }

    private static string GetPrivateKeyName(string kid)
    {
        return $"rsakey:private:{kid}";
    }

    private static string GetPublicKeyName(string kid)
    {
        return $"rsakey:public:{kid}";
    }

    private List<string> LoadKeyOrder(bool includeRaw = false)
    {
        string? serialized = _cache.GetString(KeyIndexKey);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return [];
        }

        List<string>? kids = _jsonService.Deserialize<List<string>>(serialized);
        if (kids is null)
        {
            return [];
        }

        return includeRaw ? kids : [.. kids.Where(static x => !string.IsNullOrWhiteSpace(x))];
    }

    private void SaveKeyOrder(List<string> kids)
    {
        _cache.SetString(KeyIndexKey, _jsonService.Serialize(kids), CreateTtlOptions());
    }

    private static DistributedCacheEntryOptions CreateTtlOptions()
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = KeyTtl
        };
    }

    private static byte[] ReadMasterKey(IConfiguration configuration)
    {
        string? raw = configuration["Auth:RsaMasterKey"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Missing configuration Auth:RsaMasterKey (base64, 32 bytes).");
        }

        byte[] key = Convert.FromBase64String(raw);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("Auth:RsaMasterKey must be a base64-encoded 32-byte key.");
        }

        return key;
    }

    private string EncryptPrivateParameters(RSAParameters parameters)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(parameters);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[16];

        using AesGcm aes = new(_masterKey, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        byte[] payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        return Convert.ToBase64String(payload);
    }

    private RSAParameters DecryptPrivateParameters(string encodedPayload)
    {
        byte[] payload = Convert.FromBase64String(encodedPayload);
        if (payload.Length < 28)
        {
            throw new SecurityTokenException("Invalid encrypted RSA payload.");
        }

        byte[] nonce = payload[..12];
        byte[] tag = payload[12..28];
        byte[] cipher = payload[28..];
        byte[] plain = new byte[cipher.Length];

        using AesGcm aes = new(_masterKey, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        RSAParameters parameters = JsonSerializer.Deserialize<RSAParameters>(plain);
        if (parameters.Modulus is null || parameters.Exponent is null || parameters.D is null)
        {
            throw new SecurityTokenException("Failed to restore RSA private key.");
        }

        return parameters;
    }
}
