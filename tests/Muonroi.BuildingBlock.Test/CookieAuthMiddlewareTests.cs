namespace Muonroi.BuildingBlock.Test;

public class CookieAuthMiddlewareTests
{
    [Fact]
    public async Task Middleware_Sets_Authorization_Header_From_Cookie()
    {
        DefaultHttpContext context = new();
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            EnableCookieAuth = true,
            UseRsa = false
        };

        string token = "jwt_token";
        string encrypted = MCryptographyExtension.Encrypt(info.SymmetricSecretKey, token);
        context.Request.Headers.Append("Cookie", $"{info.CookieName}={encrypted}");

        bool called = false;

        Task Next(HttpContext ctx)
        {
            called = true;
            Assert.Equal($"Bearer {token}", ctx.Request.Headers.Authorization.ToString());
            return Task.CompletedTask;
        }

        MCookieAuthMiddleware middleware = new(Next, info);
        await middleware.Invoke(context);

        Assert.True(called);
    }
}
