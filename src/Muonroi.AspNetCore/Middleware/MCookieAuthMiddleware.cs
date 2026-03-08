using Muonroi.Core.Extensions;

namespace Muonroi.AspNetCore.Middleware;

/// <summary>
/// Middleware for handling cookie-based authentication.
/// </summary>
/// <param name="next">The next delegate in the middleware pipeline.</param>
/// <param name="logger">The logger for this middleware.</param>
public class MCookieAuthMiddleware(RequestDelegate next, IMLog<MCookieAuthMiddleware> logger)
{
    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="tokenInfo">The token information for cookie decryption.</param>
    /// <returns>A task that represents the completion of the middleware invocation.</returns>
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
                logger.Error(ex, "Failed to decrypt auth cookie.");
            }
        }

        await next(context);
    }
}
