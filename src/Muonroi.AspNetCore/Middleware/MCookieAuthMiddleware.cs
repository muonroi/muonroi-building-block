using Muonroi.Core.Extensions;

namespace Muonroi.AspNetCore.Middleware;

public class MCookieAuthMiddleware(RequestDelegate next, ILogger<MCookieAuthMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, MTokenInfo tokenInfo)
    {
        string cookieName = tokenInfo.CookieName;
        if (context.Request.Cookies.TryGetValue(cookieName, out string? encryptedToken))
        {
            try
            {
                string token = MCryptographyExtension.Decrypt(tokenInfo.SymmetricSecretKey, encryptedToken);
                context.Request.Headers.Authorization = $"Bearer {token}";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to decrypt auth cookie.");
            }
        }

        await next(context);
    }
}
