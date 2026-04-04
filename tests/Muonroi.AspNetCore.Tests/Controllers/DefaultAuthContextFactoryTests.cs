using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Muonroi.AspNetCore.Controllers;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using NSubstitute;
using Xunit;
using Muonroi.Core.Abstractions.Models.Common;
using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Constants;

namespace Muonroi.AspNetCore.Tests.Controllers;

public class DefaultAuthContextFactoryTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ResourceSetting _resourceSetting;
    private readonly IConfiguration _configuration;
    private readonly TestDbContext _dbContext;
    private readonly DefaultAuthContextFactory _factory;

    public DefaultAuthContextFactoryTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _resourceSetting = new ResourceSetting();
        _configuration = Substitute.For<IConfiguration>();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new TestDbContext(options);
        _factory = new DefaultAuthContextFactory(_httpContextAccessor, _resourceSetting, _configuration, _dbContext);
    }

    [Fact]
    public void Create_NoHttpContext_ReturnsUnauthenticated()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext)null!);
        var context = _factory.Create();
        Assert.False(context.IsAuthenticated);
    }

    [Fact]
    public void Create_WithHttpContext_Unauthenticated_ReturnsMAuth()
    {
        var httpContext = new DefaultHttpContext();
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var context = _factory.Create();
        
        Assert.False(context.IsAuthenticated);
        Assert.NotNull(context.CorrelationId);
    }

    [Fact]
    public void Create_WithAmqpContext_ReturnsAuthenticated()
    {
        var amqpContext = Substitute.For<Core.Abstractions.Interfaces.IAmqpContext>();
        amqpContext.GetHeaderByKey(ClaimConstants.AccessToken).Returns("some-token");
        var factory = new DefaultAuthContextFactory(_httpContextAccessor, _resourceSetting, _configuration, _dbContext, amqpContext);
        _httpContextAccessor.HttpContext.Returns((HttpContext)null!);

        var context = factory.Create();

        Assert.True(context.IsAuthenticated);
    }
}
