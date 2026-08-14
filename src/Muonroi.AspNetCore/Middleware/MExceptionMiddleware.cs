namespace Muonroi.AspNetCore.Middleware;

/// <summary>
/// Middleware for handling unhandled exceptions and providing a standardized error response.
/// </summary>
/// <param name="next">The next delegate in the middleware pipeline.</param>
public class MExceptionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = MGuard.NotNull(next);

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="logger">The logger for this middleware.</param>
    /// <param name="serializeService">The JSON serialization service.</param>
    /// <param name="authContext">The authentication info context.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="tenantContext">The optional tenant context.</param>
    /// <param name="causalChainOptions">Optional causal chain options. When provided, a serialized chain is written to the muonroi-causal-chain response header on MException errors.</param>
    /// <returns>A task that represents the completion of the middleware invocation.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        IMLog<MExceptionMiddleware> logger,
        IMJsonSerializeService serializeService,
        MAuthenticateInfoContext authContext,
        IHostEnvironment environment,
        ITenantContext? tenantContext = null,
        IOptions<MCausalChainOptions>? causalChainOptions = null)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, logger, serializeService, authContext, environment, tenantContext, causalChainOptions?.Value);
        }
    }

    private Task HandleExceptionAsync(
        HttpContext context,
        Exception ex,
        IMLog<MExceptionMiddleware> logger,
        IMJsonSerializeService serializeService,
        MAuthenticateInfoContext authContext,
        IHostEnvironment environment,
        ITenantContext? tenantContext,
        MCausalChainOptions? causalChainOptions = null)
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

        // Layer detection logic
        string layer = GetLayer(context);
        string? tenantId = tenantContext?.TenantId ?? authContext.TenantId;
        string? userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? context.User?.FindFirst("sub")?.Value
                        ?? authContext.CurrentUserGuid;

        // Enrich structured log scope (EXC-03, EXC-04, EXC-05)
        var logScope = new Dictionary<string, object?>
        {
            ["Layer"] = layer,
            ["TenantId"] = tenantId,
            ["UserId"] = userId,
        };

        if (ex is MException mExInfo)
        {
            // D-07: Override HTTP-level layer with architectural layer from MException
            logScope["Layer"] = mExInfo.Layer.ToString();
            logScope["ErrorCode"] = mExInfo.ErrorCode;
            logScope["SourcePackage"] = mExInfo.SourcePackage;
            logScope["CallerMethod"] = mExInfo.CallerMethod;
            logScope["CallerFile"] = mExInfo.CallerFile;
            logScope["CallerLine"] = mExInfo.CallerLine;
            logScope["MTraceId"] = mExInfo.TraceId;
            logScope["MSpanId"] = mExInfo.SpanId;
        }

        using (logger.BeginScope(logScope))
        {
            if (ex is MException mex)
            {
                // D-06: Log with layer-enriched format
                if (mex.Category is MExceptionCategory.Validation or MExceptionCategory.Domain)
                {
                    logger.Warn("{ErrorCode} | {Layer} | {SourcePackage}.{CallerMethod} | {Message}", mex.ErrorCode, mex.Layer, mex.SourcePackage, mex.CallerMethod, mex.Message);
                }
                else
                {
                    logger.Error(mex, "{ErrorCode} | {Layer} | {SourcePackage}.{CallerMethod} | {Message}", mex.ErrorCode, mex.Layer, mex.SourcePackage, mex.CallerMethod, mex.Message);
                }

                context.Response.StatusCode = mex.HttpStatusCode;

                var response = new MErrorResponse
                {
                    StatusCode = mex.HttpStatusCode,
                    ErrorCode = mex.ErrorCode,
                    TraceId = traceId,
                    Message = mex.Message,
                    Details = environment.IsDevelopment() ? mex.Details : null
                };

                // Special handling for MValidationException
                if (mex is MValidationException validationEx)
                {
                    response.ErrorCode = "VALIDATION_FAILED";
                    response.Errors = [.. validationEx.Errors.Select(e => new MErrorDetail
                    {
                        Field = e.Field,
                        Message = e.Message,
                        AttemptedValue = environment.IsDevelopment() ? e.AttemptedValue : null
                    })];
                }

                // Per D-06: Serialize causal chain to response header for upstream consumers.
                // CRITICAL: Must be set BEFORE WriteAsync — headers cannot be modified after body starts.
                if (causalChainOptions is not null && causalChainOptions.PropagateInHeaders)
                {
                    MCausalChain chain = new()
                    {
                        OriginService = causalChainOptions.ServiceName,
                        OriginErrorCode = mex.ErrorCode,
                        OriginLayer = mex.Layer,
                        OriginMessage = causalChainOptions.IncludeMessageInChain ? mex.Message : null,
                        Cause = mex.CausalChain,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    context.Response.Headers["muonroi-causal-chain"] = MCausalChain.Serialize(chain);
                }

                return context.Response.WriteAsync(serializeService.Serialize(response));
            }

            // Branch 2 — FluentValidation.ValidationException
            if (ex is ValidationException fluentEx)
            {
                logger.Warn("Validation exception from FluentValidation. Message: {Message}", fluentEx.Message);
                context.Response.StatusCode = 400;

                var response = new MErrorResponse
                {
                    StatusCode = 400,
                    ErrorCode = "VALIDATION_FAILED",
                    TraceId = traceId,
                    Message = "One or more validation failures have occurred.",
                    Errors = [.. fluentEx.Errors.Select(e => new MErrorDetail
                    {
                        Field = e.PropertyName,
                        Message = e.ErrorMessage,
                        AttemptedValue = environment.IsDevelopment() ? e.AttemptedValue : null
                    })]
                };
                return context.Response.WriteAsync(serializeService.Serialize(response));
            }

            // Branch 3 — Fallback (untyped Exception):
            logger.Error(ex, "An unhandled exception occurred.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var fallback = new MErrorResponse
            {
                StatusCode = 500,
                ErrorCode = "UNHANDLED_EXCEPTION",
                TraceId = traceId,
                Message = environment.IsDevelopment() ? ex.Message : "An unexpected error occurred."
            };
            return context.Response.WriteAsync(serializeService.Serialize(fallback));
        }
    }

    private static string GetLayer(HttpContext context)
    {
        if (context.Request.Headers.ContentType.ToString().Contains("application/grpc", StringComparison.OrdinalIgnoreCase))
        {
            return "grpc";
        }

        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            return "api";
        }

        return "web";
    }
}
