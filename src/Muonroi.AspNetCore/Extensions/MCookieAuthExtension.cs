using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Extensions;

namespace Muonroi.AspNetCore.Extensions;

/// <inheritdoc />
public static class MCookieAuthExtension
{
/// <inheritdoc />
    public static void AppendAuthCookie(this HttpResponse response, string token, MTokenInfo info)
    {
        MGuard.NotEmpty(token);

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
