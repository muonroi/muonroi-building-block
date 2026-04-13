using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.AspNetCore.Tests.Extensions;

public class MCookieAuthExtensionTests
{
    [Fact]
    public void AppendAuthCookie_EmptyToken_ThrowsArgumentException()
    {
        var httpContext = new DefaultHttpContext();
        var info = new MTokenInfo { EnableCookieAuth = true, SymmetricSecretKey = "test-key-1234567890123456" };

        var act = () => httpContext.Response.AppendAuthCookie("", info);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void AppendAuthCookie_NullToken_ThrowsArgumentException()
    {
        var httpContext = new DefaultHttpContext();
        var info = new MTokenInfo { EnableCookieAuth = true, SymmetricSecretKey = "test-key-1234567890123456" };

        var act = () => httpContext.Response.AppendAuthCookie(null!, info);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void AppendAuthCookie_CookieAuthDisabled_DoesNotAppendCookie()
    {
        var httpContext = new DefaultHttpContext();
        var info = new MTokenInfo { EnableCookieAuth = false };

        httpContext.Response.AppendAuthCookie("some-token", info);

        // No exception should occur and no cookie is set
        httpContext.Response.Headers.SetCookie.Count.Should().Be(0);
    }
}
