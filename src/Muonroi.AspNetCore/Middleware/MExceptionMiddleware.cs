using FluentValidation;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Observability.OpenTelemetry;

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
    private readonly RequestDelegate _next = MGuard.NotNull(next);
    private readonly IMLog<MExceptionMiddleware> _logger = MGuard.NotNull(logger);
    private readonly IMJsonSerializeService _serializeService = MGuard.NotNull(serializeService);
    private readonly MAuthenticateInfoContext _authContext = MGuard.NotNull(authContext);
    private readonly IHostEnvironment _environment = MGuard.NotNull(environment);

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
        MGuard.NotNull(ex);

        context.Response.ContentType = "application/json";
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // D-03: Track exception metric
        if (ex is MException mexTrack)
        {
            MuonroiMetrics.ExceptionCount.Add(1, 
                new KeyValuePair<string, object?>("category", mexTrack.Category.ToString()),
                new KeyValuePair<string, object?>("error_code", mexTrack.ErrorCode));
        }

        // Branch 1 — MException (per D-08, D-09)
        if (ex is MException mex)
        {
            // Enrich structured log scope with auto-context properties (per D-03)
            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["ErrorCode"] = mex.ErrorCode,
                ["SourcePackage"] = mex.SourcePackage,
                ["CallerMethod"] = mex.CallerMethod,
                ["CallerFile"] = mex.CallerFile,
                ["CallerLine"] = mex.CallerLine,
                ["MTraceId"] = mex.TraceId,
                ["MSpanId"] = mex.SpanId,
            }))
            {
                // Log level by category (per D-08)
                if (mex.Category is MExceptionCategory.Validation or MExceptionCategory.Domain)
                    _logger.Warn("Domain/Validation exception: {ErrorCode} from {SourcePackage}.{CallerMethod}. Message: {Message}", mex.ErrorCode, mex.SourcePackage, mex.CallerMethod, mex.Message);
                else
                    _logger.Error(mex, "Infrastructure/Security exception: {ErrorCode} from {SourcePackage}.{CallerMethod}", mex.ErrorCode, mex.SourcePackage, mex.CallerMethod);
            }

            context.Response.StatusCode = mex.HttpStatusCode;

            var response = new MErrorResponse
            {
                StatusCode = mex.HttpStatusCode,
                ErrorCode = mex.ErrorCode,
                TraceId = traceId,
                Message = mex.Message,
                Details = _environment.IsDevelopment() ? mex.Details : null
            };

            // Special handling for MValidationException (per D-10)
            if (mex is MValidationException validationEx)
            {
                response.ErrorCode = "VALIDATION_FAILED";
                response.Errors = validationEx.Errors.Select(e => new MErrorDetail
                {
                    Field = e.Field,
                    Message = e.Message,
                    AttemptedValue = _environment.IsDevelopment() ? e.AttemptedValue : null
                }).ToList();
            }

            return context.Response.WriteAsync(_serializeService.Serialize(response));
        }

        // Branch 2 — FluentValidation.ValidationException (per D-10)
        if (ex is FluentValidation.ValidationException fluentEx)
        {
            _logger.Warn("Validation exception from FluentValidation. Message: {Message}", fluentEx.Message);
            context.Response.StatusCode = 400;

            var response = new MErrorResponse
            {
                StatusCode = 400,
                ErrorCode = "VALIDATION_FAILED",
                TraceId = traceId,
                Message = "One or more validation failures have occurred.",
                Errors = fluentEx.Errors.Select(e => new MErrorDetail
                {
                    Field = e.PropertyName,
                    Message = e.ErrorMessage,
                    AttemptedValue = _environment.IsDevelopment() ? e.AttemptedValue : null
                }).ToList()
            };
            return context.Response.WriteAsync(_serializeService.Serialize(response));
        }

        // Branch 3 — Fallback (untyped Exception):
        _logger.Error(ex, "An unhandled exception occurred.");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var fallback = new MErrorResponse
        {
            StatusCode = 500,
            ErrorCode = "UNHANDLED_EXCEPTION",
            TraceId = traceId,
            Message = _environment.IsDevelopment() ? ex.Message : MVoidMethodResult.GetErrorMessage(nameof(SystemEnum.UnhandledException), _authContext.Language)
        };
        return context.Response.WriteAsync(_serializeService.Serialize(fallback));
    }
}
