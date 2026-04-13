namespace Muonroi.AspNetCore.Filters;

/// <summary>
/// MVC filter that handles unhandled exceptions and returns a ProblemDetails response.
/// In non-development environments, the exception detail is sanitized to prevent information disclosure.
/// </summary>
public class GlobalExceptionFilter(IMLog<GlobalExceptionFilter> logger, IHostEnvironment environment) : AspNetExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        logger.Error(context.Exception, "Unhandled exception");
        ProblemDetails details = new()
        {
            Title = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = environment.IsDevelopment() ? context.Exception.Message : "An unexpected error occurred"
        };
        context.Result = new ObjectResult(details) { StatusCode = details.Status };
        context.ExceptionHandled = true;
    }
}
