namespace Muonroi.AspNetCore.Tests;

public class MHttpContextExtensionTests
{
    [Fact]
    public void GetRequestedIpAddress_Returns_Header_Value()
    {
        DefaultHttpContext context = new();
        context.Request.Headers["X-FORWARDED-FOR"] = "1.2.3.4";
        context.Connection.RemoteIpAddress = IPAddress.Parse("5.6.7.8");

        string result = context.GetRequestedIpAddress();

        Assert.Equal("1.2.3.4", result);
    }

    [Fact]
    public void GetRequestedIpAddress_Returns_RemoteIp_When_Header_Missing()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("5.6.7.8");

        string result = context.GetRequestedIpAddress();

        Assert.Equal("5.6.7.8", result);
    }

    [Fact]
    public void GetRequestedIpAddress_No_Ip_Returns_Empty()
    {
        DefaultHttpContext context = new();

        string result = context.GetRequestedIpAddress();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetRequestedIpAddress_Null_Context_Returns_Empty()
    {
        string result = MHttpContextExtension.GetRequestedIpAddress(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetRequestedIpAddress_Null_Connection_Returns_Empty()
    {
        HttpContext context = Substitute.For<HttpContext>();
        HttpRequest request = Substitute.For<HttpRequest>();
        request.Headers.Returns(new HeaderDictionary());
        context.Request.Returns(request);
        context.Connection.Returns((ConnectionInfo)null!);

        string result = context.GetRequestedIpAddress();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetHeaderUserAgent_Returns_Value()
    {
        DefaultHttpContext context = new();
        context.Request.Headers[HeaderNames.UserAgent] = "agent";

        string result = context.GetHeaderUserAgent();

        Assert.Equal("agent", result);
    }

    [Fact]
    public void GetHeaderUserAgent_Header_Missing_Returns_Empty()
    {
        DefaultHttpContext context = new();

        string result = context.GetHeaderUserAgent();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetHeaderUserAgent_Null_Context_Throws()
    {
        Assert.Throws<NullReferenceException>(() => MHttpContextExtension.GetHeaderUserAgent(null!));
    }
}
