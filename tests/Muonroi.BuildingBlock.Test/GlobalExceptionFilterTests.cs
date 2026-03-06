namespace Muonroi.BuildingBlock.Test;

public class GlobalExceptionFilterTests
{
    [Fact]
    public void OnException_Sets_ProblemDetails()
    {
        GlobalExceptionFilter filter = new(NullLogger<GlobalExceptionFilter>.Instance);
        DefaultHttpContext ctx = new();
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ExceptionContext ec = new(ac, [])
        {
            Exception = new InvalidOperationException("oops")
        };
        filter.OnException(ec);
        ObjectResult result = Assert.IsType<ObjectResult>(ec.Result);
        ProblemDetails details = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status500InternalServerError, details.Status);
        Assert.Equal("oops", details.Detail);
        Assert.True(ec.ExceptionHandled);
    }

    [Fact]
    public void OnException_Null_Exception_Throws()
    {
        GlobalExceptionFilter filter = new(NullLogger<GlobalExceptionFilter>.Instance);
        DefaultHttpContext ctx = new();
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ExceptionContext ec = new(ac, [])
        {
            Exception = null!
        };
        Assert.Throws<NullReferenceException>(() => filter.OnException(ec));
    }

    [Fact]
    public void OnException_Handled_Still_Processes()
    {
        GlobalExceptionFilter filter = new(NullLogger<GlobalExceptionFilter>.Instance);
        DefaultHttpContext ctx = new();
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ExceptionContext ec = new(ac, [])
        {
            Exception = new Exception("e"),
            ExceptionHandled = true
        };
        filter.OnException(ec);
        Assert.True(ec.ExceptionHandled);
        Assert.IsType<ObjectResult>(ec.Result);
    }

    [Fact]
    public void Constructor_Allows_Null_Logger()
    {
        GlobalExceptionFilter filter = new(null!);
        Assert.NotNull(filter);
    }
}
