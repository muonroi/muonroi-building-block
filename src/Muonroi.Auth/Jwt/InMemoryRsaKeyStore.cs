namespace Muonroi.Auth.Jwt;

/// <summary>
/// An in-memory implementation of the IRsaKeyStore.
/// </summary>
public class InMemoryRsaKeyStore : IRsaKeyStore
{
    private readonly ConcurrentDictionary<string, RsaSecurityKey> _keys = new();
    private string _currentKid = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryRsaKeyStore"/> class and rotates the initial keys.
    /// </summary>
    public InMemoryRsaKeyStore()
    {
        RotateKeys();
    }

    /// <summary>
    /// Gets the current signing credentials using the active RSA key.
    /// </summary>
    /// <returns>The active signing credentials.</returns>
    public SigningCredentials GetCurrentSigningCredentials()
    {
        RsaSecurityKey key = _keys[_currentKid];
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// Rotates the RSA keys by creating a new active key and keeping up to two previous keys.
    /// </summary>
    public void RotateKeys()
    {
        RSA rsa = RSA.Create(2048);
        RsaSecurityKey key = new(rsa)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        _keys[key.KeyId] = key;
        _currentKid = key.KeyId;

        if (_keys.Count <= 2)
        {
            return;
        }

        List<string> toRemove =
        [
            .. _keys.Keys
                .Where(k => k != _currentKid)
                .OrderBy(k => k)
                .Take(_keys.Count - 2)
        ];
        foreach (string kid in toRemove)
        {
            _keys.TryRemove(kid, out _);
        }
    }

    /// <summary>
    /// Retrieves a specific security key by its key identifier (KID).
    /// </summary>
    /// <param name="kid">The unique key identifier.</param>
    /// <returns>The security key if found; otherwise, null.</returns>
    public SecurityKey? GetKey(string kid)
    {
        return _keys.TryGetValue(kid, out RsaSecurityKey? key) ? key : null;
    }

    /// <summary>
    /// Gets the JSON Web Key Set (JWKS) containing all public keys currently in the store.
    /// </summary>
    /// <returns>The set of JSON Web Keys.</returns>
    public JsonWebKeySet GetJsonWebKeySet()
    {
        JsonWebKeySet jsonWebKeySet = new();
        foreach (RsaSecurityKey key in _keys.Values)
        {
            JsonWebKey jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            jwk.Kid = key.KeyId;
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            jsonWebKeySet.Keys.Add(jwk);
        }

        return jsonWebKeySet;
    }
}
