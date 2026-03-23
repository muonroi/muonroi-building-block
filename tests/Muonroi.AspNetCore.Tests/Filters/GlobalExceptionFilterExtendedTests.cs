namespace Muonroi.AspNetCore.Tests.Filters;

public class GlobalExceptionFilterExtendedTests
{
    [Fact]
    public void OnException_SetsExceptionHandledTrue()
    {
        var logger = Substitute.For<IMLog<GlobalExceptionFilter>>();
        var filter = new GlobalExceptionFilter(logger);
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
        var logger = Substitute.For<IMLog<GlobalExceptionFilter>>();
        var filter = new GlobalExceptionFilter(logger);
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
        var logger = Substitute.For<IMLog<GlobalExceptionFilter>>();
        var filter = new GlobalExceptionFilter(logger);
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
        var filter = new GlobalExceptionFilter(logger);
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var exception = new Exception("test");
        var exceptionContext = new ExceptionContext(actionContext, []) { Exception = exception };

        filter.OnException(exceptionContext);

        logger.Received(1).Error(exception, "Unhandled exception");
    }
}
