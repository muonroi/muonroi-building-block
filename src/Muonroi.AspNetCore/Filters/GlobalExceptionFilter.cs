namespace Muonroi.AspNetCore.Filters;

/// <summary>
/// MVC filter that handles unhandled exceptions and returns a ProblemDetails response.
/// </summary>
public class GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) : AspNetExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        logger.LogError(context.Exception, "Unhandled exception");
        ProblemDetails details = new()
        {
            Title = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = context.Exception.Message
        };
        context.Result = new ObjectResult(details) { StatusCode = details.Status };
        context.ExceptionHandled = true;
    }
}
