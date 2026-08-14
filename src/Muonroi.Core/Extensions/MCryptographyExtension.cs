namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for cryptographic operations.
/// </summary>
public static class MCryptographyExtension
{
    /// <summary>
    /// Encrypts the specified plain text using the provided key.
    /// </summary>
    /// <param name="key">The encryption key.</param>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>The encrypted text as a base64-encoded string.</returns>
    public static string Encrypt(string key, string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        using Aes aesAlg = Aes.Create();
        aesAlg.KeySize = 256;
        aesAlg.Key = GetValidKey(key, 256);
        aesAlg.GenerateIV();
        byte[] iv = aesAlg.IV;

        using ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, iv);
        using MemoryStream msEncrypt = new();
        msEncrypt.Write(iv, 0, iv.Length);
        using (CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (StreamWriter swEncrypt = new(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    /// <summary>
    /// Decrypts the specified cipher text using the provided key.
    /// </summary>
    /// <param name="key">The decryption key.</param>
    /// <param name="cipherText">The base64-encoded cipher text to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    public static string Decrypt(string key, string cipherText)
    {
        return Decrypt(key, cipherText, string.Empty);
    }

    /// <summary>
    /// Decrypts the specified cipher text using the provided key and fingerprint salt.
    /// </summary>
    /// <param name="key">The decryption key.</param>
    /// <param name="cipherText">The base64-encoded cipher text to decrypt.</param>
    /// <param name="fingerprintSalt">The fingerprint salt to use for key generation.</param>
    /// <returns>The decrypted plain text.</returns>
    public static string Decrypt(string key, string cipherText, string fingerprintSalt)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        byte[] fullCipher = Convert.FromBase64String(cipherText);
        using Aes aesAlg = Aes.Create();
        aesAlg.KeySize = 256;

        string effectiveKey = string.IsNullOrEmpty(fingerprintSalt)
            ? key
            : GenerateSha256String(key + fingerprintSalt);

        aesAlg.Key = GetValidKey(effectiveKey, 256);

        byte[] iv = new byte[16];
        byte[] cipher = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
        aesAlg.IV = iv;

        using ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        using MemoryStream msDecrypt = new(cipher);
        using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
        using StreamReader srDecrypt = new(csDecrypt);
        return srDecrypt.ReadToEnd();
    }

    private static byte[] GetValidKey(string key, int keySize)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] hashBytes = SHA256.HashData(keyBytes);
        byte[] validKey = new byte[keySize / 8];
        Buffer.BlockCopy(hashBytes, 0, validKey, 0, validKey.Length);
        return validKey;
    }

    /// <summary>
    /// Generates a SHA256 hash string for the specified input string.
    /// </summary>
    /// <param name="inputString">The input string to hash.</param>
    /// <returns>A lowercase hexadecimal string representation of the SHA256 hash.</returns>
    public static string GenerateSha256String(string inputString)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(inputString));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets a hash representing the integrity of the specified method body.
    /// </summary>
    /// <param name="method">The method to check for integrity.</param>
    /// <returns>A hexadecimal string representation of the IL body hash, or a status message if the check fails.</returns>
    public static string GetMethodIntegrityHash(MethodBase method)
    {
        try
        {
            MethodBody? body = method.GetMethodBody();
            if (body == null)
            {
                return "NATIVE_OR_JIT_PROTECTED";
            }

            byte[]? ilBytes = body.GetILAsByteArray();
            if (ilBytes == null)
            {
                return "EMPTY_BODY";
            }

            byte[] hash = SHA256.HashData(ilBytes);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return "INTEGRITY_CHECK_FAILED";
        }
    }
}
