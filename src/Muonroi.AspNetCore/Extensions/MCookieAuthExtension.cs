using Muonroi.Core.Extensions;

namespace Muonroi.AspNetCore.Extensions;

public static class MCookieAuthExtension
{
    public static void AppendAuthCookie(this HttpResponse response, string token, MTokenInfo info)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentException("Token cannot be null or empty", nameof(token));
        }

        if (!info.EnableCookieAuth)
        {
            return;
        }

        string encrypted = MCryptographyExtension.Encrypt(info.SymmetricSecretKey, token);
        CookieOptions options = new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = Enum.TryParse(info.CookieSameSite, true, out Microsoft.AspNetCore.Http.SameSiteMode same)
                ? same
                : Microsoft.AspNetCore.Http.SameSiteMode.Lax
        };
        response.Cookies.Append(info.CookieName, encrypted, options);
    }
}
