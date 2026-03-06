using System.Text;

namespace Muonroi.Core.Extensions;

public static class MCryptographyExtension
{
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

    public static string Decrypt(string key, string cipherText)
    {
        return Decrypt(key, cipherText, string.Empty);
    }

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

    public static string GenerateSha256String(string inputString)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(inputString));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

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
