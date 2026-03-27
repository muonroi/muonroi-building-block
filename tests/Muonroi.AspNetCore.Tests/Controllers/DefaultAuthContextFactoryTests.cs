using Microsoft.EntityFrameworkCore;
using IAmqpContext = Muonroi.Core.Abstractions.Interfaces.IAmqpContext;

namespace Muonroi.AspNetCore.Tests.Controllers;

public class DefaultAuthContextFactoryTests
{
    private static MDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<MDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var mediator = Substitute.For<IMediator>();
        return new MDbContext(options, mediator);
    }

    [Fact]
    public void Create_NoHttpContext_NoAmqp_ReturnsUnauthenticated()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var resourceSetting = new ResourceSetting();
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDb();

        var factory = new DefaultAuthContextFactory(httpContextAccessor, resourceSetting, config, db);
        var result = factory.Create();

        result.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Create_WithHttpContext_NotAuthenticated_SetsLanguageAndCorrelation()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.AcceptLanguage = "en-US";
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        var resourceSetting = new ResourceSetting();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ClaimConstants.ApiKey] = "test-api-key" })
            .Build();
        using var db = CreateInMemoryDb();

        var factory = new DefaultAuthContextFactory(httpContextAccessor, resourceSetting, config, db);
        var result = factory.Create();

        result.Should().BeAssignableTo<MAuthenticateInfoContext>();
        var mAuth = (MAuthenticateInfoContext)result;
        mAuth.Language.Should().Be("en-US");
        mAuth.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public void Create_WithHttpContext_DefaultLanguage_IsVietnamese()
    {
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        var resourceSetting = new ResourceSetting();
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDb();

        var factory = new DefaultAuthContextFactory(httpContextAccessor, resourceSetting, config, db);
        var result = factory.Create();

        var mAuth = (MAuthenticateInfoContext)result;
        mAuth.Language.Should().Be("vi-VN");
    }

    [Fact]
    public void Create_WithAmqpContext_ReturnsAuthFromHeaders()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var amqpContext = Substitute.For<IAmqpContext>();
        amqpContext.GetHeaderByKey(CustomHeader.CorrelationId).Returns("corr-123");
        amqpContext.GetHeaderByKey(ClaimConstants.UserIdentifier).Returns("user-guid-123");
        amqpContext.GetHeaderByKey(ClaimConstants.Username).Returns("amqp-user");
        amqpContext.GetHeaderByKey(CustomHeader.TenantId).Returns("amqp-tenant");
        amqpContext.GetHeaderByKey(ClaimConstants.AccessToken).Returns("amqp-token");
        var resourceSetting = new ResourceSetting();
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDb();

        var factory = new DefaultAuthContextFactory(httpContextAccessor, resourceSetting, config, db, amqpContext);
        var result = factory.Create();

        result.IsAuthenticated.Should().BeTrue();
        result.CurrentUserGuid.Should().Be("user-guid-123");
        result.CurrentUsername.Should().Be("amqp-user");
        result.TenantId.Should().Be("amqp-tenant");
    }

    [Fact]
    public void Create_WithAmqpContext_NoToken_IsNotAuthenticated()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var amqpContext = Substitute.For<IAmqpContext>();
        amqpContext.GetHeaderByKey(Arg.Any<string>()).Returns((string?)null);
        var resourceSetting = new ResourceSetting();
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDb();

        var factory = new DefaultAuthContextFactory(httpContextAccessor, resourceSetting, config, db, amqpContext);
        var result = factory.Create();

        result.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Create_WithCorrelationIdHeader_UsesHeaderValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CustomHeader.CorrelationId] = "my-correlation-id";
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        var resourceSetting = new ResourceSetting();
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDb();

        var factory = new DefaultAuthContextFactory(httpContextAccessor, resourceSetting, config, db);
        var result = factory.Create();

        result.CorrelationId.Should().Be("my-correlation-id");
    }
}
