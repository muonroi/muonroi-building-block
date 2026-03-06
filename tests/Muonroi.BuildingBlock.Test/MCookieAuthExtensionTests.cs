namespace Muonroi.BuildingBlock.Test;

public class MCookieAuthExtensionTests
{
    [Fact]
    public void AppendAuthCookie_Adds_Encrypted_Cookie_When_Enabled()
    {
        DefaultHttpContext ctx = new();
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "key123456789012345678901234567890",
            EnableCookieAuth = true
        };
        string token = "jwt";

        ctx.Response.AppendAuthCookie(token, info);

        string header = ctx.Response.Headers.SetCookie!;
        // Extract the value part: AuthToken=...; path=/; ...
        string cookieValue = header.Split(';')[0].Split('=')[1];
        string decrypted = MCryptographyExtension.Decrypt(info.SymmetricSecretKey, Uri.UnescapeDataString(cookieValue));
        Assert.Equal(token, decrypted);
    }

    [Fact]
    public void AppendAuthCookie_Null_Response_Throws()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "k",
            EnableCookieAuth = true
        };
        Assert.ThrowsAny<Exception>(() => MCookieAuthExtension.AppendAuthCookie(null!, "t", info));
    }

    [Fact]
    public void AppendAuthCookie_Null_Token_Throws()
    {
        DefaultHttpContext ctx = new();
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "key123456789012345678901234567890",
            EnableCookieAuth = true
        };
        Assert.Throws<ArgumentException>(() => ctx.Response.AppendAuthCookie(null!, info));
    }

    [Fact]
    public void AppendAuthCookie_Overwrites_Existing_Cookie()
    {
        DefaultHttpContext ctx = new();
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "key123456789012345678901234567890",
            EnableCookieAuth = true
        };
        ctx.Response.Cookies.Append(info.CookieName, "old");

        ctx.Response.AppendAuthCookie("new", info);

        string header = ctx.Response.Headers.SetCookie!;
        // The last AuthToken in the list should be the new one (browsers/EF core might append)
        string lastCookie = header.Split(',').AsEnumerable().Last();
        string cookieValue = lastCookie.Split(';')[0].Split('=')[1];
        string decrypted = MCryptographyExtension.Decrypt(info.SymmetricSecretKey, Uri.UnescapeDataString(cookieValue));
        Assert.Equal("new", decrypted);
    }
}
