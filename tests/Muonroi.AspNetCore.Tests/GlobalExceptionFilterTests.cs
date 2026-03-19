namespace Muonroi.AspNetCore.Tests;

public class GlobalExceptionFilterTests
{
    [Fact]
    public void OnException_Sets_ProblemDetails_And_Marks_Handled()
    {
        GlobalExceptionFilter filter = new(Substitute.For<IMLog<GlobalExceptionFilter>>());
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
    public void Constructor_Allows_Null_Logger_For_Current_Implementation()
    {
        GlobalExceptionFilter filter = new(null!);

        filter.Should().NotBeNull();
    }
}

