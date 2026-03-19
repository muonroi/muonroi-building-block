namespace Muonroi.AspNetCore.Middleware;

/// <summary>
/// Middleware for handling unhandled exceptions and providing a standardized error response.
/// </summary>
/// <param name="next">The next delegate in the middleware pipeline.</param>
/// <param name="logger">The logger for this middleware.</param>
/// <param name="serializeService">The JSON serialization service.</param>
/// <param name="authContext">The authentication info context.</param>
/// <param name="environment">The host environment.</param>
public class MExceptionMiddleware(
    RequestDelegate next,
    IMLog<MExceptionMiddleware> logger,
    IMJsonSerializeService serializeService,
    MAuthenticateInfoContext authContext,
    IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly IMLog<MExceptionMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMJsonSerializeService _serializeService = serializeService ?? throw new ArgumentNullException(nameof(serializeService));
    private readonly MAuthenticateInfoContext _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));
    private readonly IHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the completion of the middleware invocation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception? ex)
    {
        if (ex is null)
        {
            return Task.FromException(new NullReferenceException());
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var response = new
        {
            context.Response.StatusCode,
            error = new
            {
                code = nameof(SystemEnum.UnhandledException),
                message = MVoidMethodResult.GetErrorMessage(nameof(SystemEnum.UnhandledException), _authContext.Language),
                details = _environment.IsDevelopment() ? ex.Message : null
            }
        };

        return context.Response.WriteAsync(_serializeService.Serialize(response));
    }

}
