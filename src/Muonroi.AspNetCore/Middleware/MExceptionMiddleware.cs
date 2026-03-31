using FluentValidation;
using Muonroi.Core.Abstractions.Exceptions;

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
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        // Branch 1 — MException (per D-08, D-09)
        if (ex is MException mex)
        {
            // Log level by category (per D-08)
            if (mex.Category is MExceptionCategory.Validation or MExceptionCategory.Domain)
                _logger.Warn("Domain/Validation exception: {ErrorCode}. Message: {Message}", mex.ErrorCode, mex.Message);
            else
                _logger.Error(mex, "Infrastructure/Security exception: {ErrorCode}", mex.ErrorCode);

            context.Response.StatusCode = mex.HttpStatusCode;

            // Special handling for MValidationException (per D-10)
            if (mex is MValidationException validationEx)
            {
                var body = new
                {
                    statusCode = 400,
                    errorCode = "VALIDATION_FAILED",
                    traceId = Activity.Current?.Id ?? context.TraceIdentifier,
                    errors = validationEx.Errors.Select(e => new { field = e.Field, message = e.Message, attemptedValue = _environment.IsDevelopment() ? e.AttemptedValue : null })
                };
                return context.Response.WriteAsync(_serializeService.Serialize(body));
            }

            // General MException response (per D-09)
            var response = new
            {
                statusCode = mex.HttpStatusCode,
                errorCode = mex.ErrorCode,
                traceId = Activity.Current?.Id ?? context.TraceIdentifier,
                message = mex.Message,
                details = _environment.IsDevelopment() ? mex.Details : null
            };
            return context.Response.WriteAsync(_serializeService.Serialize(response));
        }

        // Branch 2 — FluentValidation.ValidationException (per D-10)
        if (ex is FluentValidation.ValidationException fluentEx)
        {
            _logger.Warn("Validation exception from FluentValidation. Message: {Message}", fluentEx.Message);
            context.Response.StatusCode = 400;

            var body = new
            {
                statusCode = 400,
                errorCode = "VALIDATION_FAILED",
                traceId = Activity.Current?.Id ?? context.TraceIdentifier,
                errors = fluentEx.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage, attemptedValue = _environment.IsDevelopment() ? e.AttemptedValue : null })
            };
            return context.Response.WriteAsync(_serializeService.Serialize(body));
        }

        // Branch 3 — Fallback (untyped Exception):
        _logger.Error(ex, "An unhandled exception occurred.");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var fallback = new
        {
            statusCode = 500,
            errorCode = "UNHANDLED_EXCEPTION",
            traceId = Activity.Current?.Id ?? context.TraceIdentifier,
            message = _environment.IsDevelopment() ? ex.Message : MVoidMethodResult.GetErrorMessage(nameof(SystemEnum.UnhandledException), _authContext.Language)
        };
        return context.Response.WriteAsync(_serializeService.Serialize(fallback));
    }

}
