namespace Muonroi.Auth.Jwt;

/// <summary>
/// Interface for a store that manages RSA keys for JWT signing and validation.
/// </summary>
public interface IRsaKeyStore
{
    /// <summary>
    /// Gets the current signing credentials using the active RSA key.
    /// </summary>
    /// <returns>The active signing credentials.</returns>
    SigningCredentials GetCurrentSigningCredentials();

    /// <summary>
    /// Rotates the RSA keys by creating a new active key and potentially retiring old ones.
    /// </summary>
    void RotateKeys();

    /// <summary>
    /// Retrieves a specific security key by its key identifier (KID).
    /// </summary>
    /// <param name="kid">The unique key identifier.</param>
    /// <returns>The security key if found; otherwise, null.</returns>
    SecurityKey? GetKey(string kid);

    /// <summary>
    /// Gets the JSON Web Key Set (JWKS) containing all public keys.
    /// </summary>
    /// <returns>The set of JSON Web Keys.</returns>
    JsonWebKeySet GetJsonWebKeySet();
}
