using System.Globalization;
using System.Text;

namespace Muonroi.Core.Extensions;

public static class MStringExtension
{
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

    public static string? Truncate(this string? str, int maxLength)
    {
        if (str?.Length > maxLength)
        {
            return str is null ? throw new ArgumentNullException(nameof(str)) :
            str.Left(maxLength);
        }
        else
        {
            return str is null ? throw new ArgumentNullException(nameof(str)) :
            str;
        }
    }

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

    public static string ToBase64String(this string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string FromBase64String(this string base64EncodedData)
    {
        if (string.IsNullOrEmpty(base64EncodedData))
        {
            return string.Empty;
        }

        byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }

    public static string Left(this string? str, int len)
    {
        ArgumentNullException.ThrowIfNull(str);

        if (str.Length < len)
        {
            throw new ArgumentException("len argument can not be bigger than given string's length!");
        }

        return str[..len];
    }
}
