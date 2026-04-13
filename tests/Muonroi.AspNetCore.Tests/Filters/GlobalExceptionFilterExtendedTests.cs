namespace Muonroi.AspNetCore.Tests.Filters;

public class GlobalExceptionFilterExtendedTests
{
    private static GlobalExceptionFilter CreateFilter(string environmentName = "Development")
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return new GlobalExceptionFilter(Substitute.For<IMLog<GlobalExceptionFilter>>(), env);
    }

    [Fact]
    public void OnException_SetsExceptionHandledTrue()
    {
        var filter = CreateFilter();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = new InvalidOperationException("test error")
        };

        filter.OnException(exceptionContext);

        exceptionContext.ExceptionHandled.Should().BeTrue();
    }

    [Fact]
    public void OnException_ReturnsObjectResultWith500()
    {
        var filter = CreateFilter();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = new Exception("some failure")
        };

        filter.OnException(exceptionContext);

        exceptionContext.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)exceptionContext.Result!;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void OnException_ResultContainsProblemDetails()
    {
        // Development environment: detail exposed
        var filter = CreateFilter("Development");
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = new Exception("detailed error message")
        };

        filter.OnException(exceptionContext);

        var objectResult = (ObjectResult)exceptionContext.Result!;
        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Detail.Should().Be("detailed error message");
        problemDetails.Title.Should().Contain("error occurred");
    }

    [Fact]
    public void OnException_LogsError()
    {
        var logger = Substitute.For<IMLog<GlobalExceptionFilter>>();
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        var filter = new GlobalExceptionFilter(logger, env);
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var exception = new Exception("test");
        var exceptionContext = new ExceptionContext(actionContext, []) { Exception = exception };

        filter.OnException(exceptionContext);

        logger.Received(1).Error(exception, "Unhandled exception");
    }
}
