namespace Muonroi.AspNetCore.Tests;

public class GlobalExceptionFilterTests
{
    [Fact]
    public void OnException_Sets_ProblemDetails_And_Marks_Handled()
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        GlobalExceptionFilter filter = new(Substitute.For<IMLog<GlobalExceptionFilter>>(), env);
        DefaultHttpContext httpContext = new();
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ExceptionContext exceptionContext = new(actionContext, [])
        {
            Exception = new InvalidOperationException("oops")
        };

        filter.OnException(exceptionContext);

        ObjectResult result = exceptionContext.Result.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails details = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Status.Should().Be(StatusCodes.Status500InternalServerError);
        details.Detail.Should().Be("oops");
        exceptionContext.ExceptionHandled.Should().BeTrue();
    }

    [Fact]
    public void OnException_Sanitizes_Detail_In_Production()
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        GlobalExceptionFilter filter = new(Substitute.For<IMLog<GlobalExceptionFilter>>(), env);
        DefaultHttpContext httpContext = new();
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ExceptionContext exceptionContext = new(actionContext, [])
        {
            Exception = new InvalidOperationException("connection string: Server=db;Password=secret-connection-string")
        };

        filter.OnException(exceptionContext);

        ObjectResult result = exceptionContext.Result.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails details = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Detail.Should().Be("An unexpected error occurred");
        details.Detail.Should().NotContain("secret");
        exceptionContext.ExceptionHandled.Should().BeTrue();
    }

    [Fact]
    public void OnException_Shows_Detail_In_Development()
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        GlobalExceptionFilter filter = new(Substitute.For<IMLog<GlobalExceptionFilter>>(), env);
        DefaultHttpContext httpContext = new();
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ExceptionContext exceptionContext = new(actionContext, [])
        {
            Exception = new InvalidOperationException("detailed dev error message")
        };

        filter.OnException(exceptionContext);

        ObjectResult result = exceptionContext.Result.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails details = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Detail.Should().Be("detailed dev error message");
    }

    [Fact]
    public void Constructor_Allows_Null_Logger_For_Current_Implementation()
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        GlobalExceptionFilter filter = new(null!, env);

        filter.Should().NotBeNull();
    }
}
