using System.Globalization;
using System.Text;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="string"/> manipulation and encryption.
/// </summary>
public static class MStringExtension
{
    /// <summary>
    /// Normalizes a string by removing accents and converting to lowercase.
    /// </summary>
    /// <param name="input">The string to normalize.</param>
    /// <returns>A normalized string.</returns>
    public static string NormalizeString(this string input)
    {
        string normalizedString = input.Normalize(NormalizationForm.FormD);
        StringBuilder stringBuilder = new();

        foreach (char c in normalizedString.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            stringBuilder.Append(c);
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("đ", "d")
            .Replace("Đ", "d")
            .Replace(" ", string.Empty)
            .Replace("đ", "d")
            .Replace("Đ", "D")
            .ToLowerInvariant();
    }

    /// <summary>
    /// Truncates a string to the specified maximum length.
    /// </summary>
    /// <param name="str">The string to truncate.</param>
    /// <param name="maxLength">The maximum length of the string.</param>
    /// <returns>The truncated string.</returns>
    /// <exception cref="MArgumentException">Thrown if <paramref name="str"/> is null.</exception>
    public static string? Truncate(this string? str, int maxLength)
    {
        MGuard.NotNull(str);

        return str.Length > maxLength
            ? str.Left(maxLength)
            : str;
    }

    /// <summary>
    /// Decrypts a configuration value if encryption is enabled.
    /// </summary>
    /// <param name="configuration">The configuration to check for encryption settings.</param>
    /// <param name="value">The value to decrypt.</param>
    /// <param name="isSecrectDefault">True to use the default secret; otherwise, false.</param>
    /// <param name="sereckey">The secret key for decryption if <paramref name="isSecrectDefault"/> is false.</param>
    /// <param name="fingerprintSalt">The fingerprint salt for decryption.</param>
    /// <returns>The decrypted value, or the original value if encryption is disabled.</returns>
    public static string? DecryptConfigurationValue(IConfiguration configuration, string? value, bool isSecrectDefault,
        string sereckey = "", string fingerprintSalt = "")
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        bool enableEncryption = configuration.GetValue("EnableEncryption", false);
        if (!enableEncryption)
        {
            return value;
        }

        return isSecrectDefault
            ? configuration.GetCryptConfigValueCipherText(value, fingerprintSalt)
            : configuration.GetCryptConfigValue(value, sereckey, fingerprintSalt);
    }

    /// <summary>
    /// Converts a plain text string to a base64-encoded string.
    /// </summary>
    /// <param name="plainText">The plain text to encode.</param>
    /// <returns>A base64-encoded string.</returns>
    public static string ToBase64String(this string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    /// <summary>
    /// Decodes a base64-encoded string to plain text.
    /// </summary>
    /// <param name="base64EncodedData">The base64-encoded data.</param>
    /// <returns>The decoded plain text.</returns>
    public static string FromBase64String(this string base64EncodedData)
    {
        if (string.IsNullOrEmpty(base64EncodedData))
        {
            return string.Empty;
        }

        byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }

    /// <summary>
    /// Returns the leftmost characters of a string up to the specified length.
    /// </summary>
    /// <param name="str">The source string.</param>
    /// <param name="len">The number of characters to return.</param>
    /// <returns>The leftmost part of the string.</returns>
    /// <exception cref="MArgumentException">Thrown if <paramref name="str"/> is null.</exception>
    /// <exception cref="MArgumentException">Thrown if <paramref name="len"/> is greater than the string length.</exception>
    public static string Left(this string? str, int len)
    {
        MGuard.NotNull(str);
        MGuard.Against(str.Length < len, "len argument can not be bigger than given string's length!");

        return str[..len];
    }
}
