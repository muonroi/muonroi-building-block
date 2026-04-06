using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Muonroi.AspNetCore.Filters;
using Muonroi.AspNetCore.Middleware;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Muonroi.AspNetCore.Tests;

public class ExceptionHandlingTests
{
    [Fact]
    public void GlobalExceptionFilter_OnException_SetsResult()
    {
        var logger = Substitute.For<IMLog<GlobalExceptionFilter>>();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        var filter = new GlobalExceptionFilter(logger, env);
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = new Exception("Test exception")
        };

        filter.OnException(exceptionContext);

        var result = Assert.IsType<ObjectResult>(exceptionContext.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.True(exceptionContext.ExceptionHandled);
    }

    [Fact]
    public async Task MExceptionMiddleware_InvokeAsync_HandlesException()
    {
        var next = Substitute.For<RequestDelegate>();
        next.Invoke(Arg.Any<HttpContext>()).Returns(x => throw new Exception("Test"));
        var logger = Substitute.For<IMLog<MExceptionMiddleware>>();
        var serializeService = Substitute.For<IMJsonSerializeService>();
        var authContext = new MAuthenticateInfoContext(false) { Language = "en" };
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var middleware = new MExceptionMiddleware(next, logger, serializeService, authContext, env);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        serializeService.Received(1).Serialize(Arg.Any<object>());
    }
}
