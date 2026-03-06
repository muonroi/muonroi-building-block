namespace Muonroi.Auth.Jwt;

public class InMemoryRsaKeyStore : IRsaKeyStore
{
    private readonly ConcurrentDictionary<string, RsaSecurityKey> _keys = new();
    private string _currentKid = string.Empty;

    public InMemoryRsaKeyStore()
    {
        RotateKeys();
    }

    public SigningCredentials GetCurrentSigningCredentials()
    {
        RsaSecurityKey key = _keys[_currentKid];
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

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

    public SecurityKey? GetKey(string kid)
    {
        return _keys.TryGetValue(kid, out RsaSecurityKey? key) ? key : null;
    }

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
