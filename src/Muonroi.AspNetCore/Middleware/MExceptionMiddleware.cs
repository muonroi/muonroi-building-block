namespace Muonroi.AspNetCore.Middleware;

public class MExceptionMiddleware(
    RequestDelegate next,
    ILogger<MExceptionMiddleware> logger,
    IMJsonSerializeService serializeService,
    MAuthenticateInfoContext authContext,
    IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<MExceptionMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMJsonSerializeService _serializeService = serializeService ?? throw new ArgumentNullException(nameof(serializeService));
    private readonly MAuthenticateInfoContext _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));
    private readonly IHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
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
