using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Muonroi.AspNetCore.Controllers;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Core.Abstractions.Models.Common;
using NSubstitute;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Controllers;

public class TestRuntimeManifestController(
    IEnumerable<IUiEngineManifestContributor> contributors,
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    IMDateTimeService dateTimeService,
    IMJsonSerializeService jsonSerializeService)
    : UiEngineRuntimeManifestControllerBase(contributors, configuration, serviceProvider, dateTimeService, jsonSerializeService)
{
}

public class UiEngineRuntimeManifestControllerBaseTests
{
    private readonly IEnumerable<IUiEngineManifestContributor> _contributors;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMDateTimeService _dateTimeService;
    private readonly IMJsonSerializeService _jsonSerializeService;
    private readonly TestRuntimeManifestController _controller;

    public UiEngineRuntimeManifestControllerBaseTests()
    {
        _contributors = new List<IUiEngineManifestContributor>();
        _configuration = Substitute.For<IConfiguration>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _dateTimeService = Substitute.For<IMDateTimeService>();
        _jsonSerializeService = Substitute.For<IMJsonSerializeService>();
        _controller = new TestRuntimeManifestController(_contributors, _configuration, _serviceProvider, _dateTimeService, _jsonSerializeService);
        
        // Mocking ControllerContext and HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = _serviceProvider;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public void GetRoot_ReturnsOk()
    {
        var result = _controller.GetRoot();
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetContractInfo_ReturnsOk()
    {
        var result = _controller.GetContractInfo();
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrent_ReturnsOk()
    {
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");
        var result = await _controller.GetCurrent(null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrent_MinimalRouting_ReturnsOk()
    {
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");
        var result = await _controller.GetCurrent("routing", CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetByUser_ReturnsOk()
    {
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");
        var result = await _controller.GetByUser(Guid.NewGuid(), null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetSchemaHash_ReturnsOk()
    {
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");
        var result = await _controller.GetSchemaHash(CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrent_WithEtag_ReturnsNotModified()
    {
        var manifest = new MUiEngineManifest();
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");
        
        // First call to get ETag
        var result1 = await _controller.GetCurrent(null, CancellationToken.None);
        var okResult1 = Assert.IsType<OkObjectResult>(result1);
        var etag = _controller.Response.Headers.ETag.ToString();

        // Second call with If-None-Match
        _controller.Request.Headers.IfNoneMatch = etag;
        var result2 = await _controller.GetCurrent(null, CancellationToken.None);
        
        Assert.IsType<StatusCodeResult>(result2);
        Assert.Equal(StatusCodes.Status304NotModified, ((StatusCodeResult)result2).StatusCode);
    }
}
