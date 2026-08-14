namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Represents the MDiagnostics Behavior.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "traceContext.Current is checked via the traceContext.Begin pattern; null-forgiving required after confirmed non-null context.")]
public sealed class MDiagnosticsBehavior<TRequest, TResponse>(
    IMLog<MDiagnosticsBehavior<TRequest, TResponse>> logger,
    IMTraceContext traceContext,
    ITraceSessionStore sessionStore,
    ISystemExecutionContextAccessor contextAccessor,
    IHttpContextAccessor? httpContextAccessor = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const string TraceHeader = "X-Muonroi-Trace";

    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ISystemExecutionContext context = contextAccessor.Get();
        ActivationLevel activationLevel = GetActivationLevel();

        if (activationLevel == ActivationLevel.None)
        {
            // Level 0: Original logging behavior
            logger.Info("[MEDIATOR] Starting {RequestName} (Session: {CorrelationId})", typeof(TRequest).Name, context.CorrelationId);
            try
            {
                return await next();
            }
            finally
            {
                logger.Info("[MEDIATOR] Finished {RequestName} in {Elapsed}ms", typeof(TRequest).Name, Stopwatch.StartNew().ElapsedMilliseconds);
            }
        }

        // Level 1 or 2: Full Diagnostic Trace
        using IDisposable sessionScope = traceContext.Begin(context.CorrelationId, context.TenantId, context.UserId, activationLevel == ActivationLevel.Deep);
        ITraceSession session = traceContext.Current!;
        using IDisposable nodeScope = session.BeginNode(typeof(TRequest).Name, MTraceNodeType.MediatorRequest);
        // Capture Request Body (unless sensitive)
        if (IsSensitive(typeof(TRequest)))
        {
            session.Record("Request body redacted (Sensitive)");
        }
        else
        {
            session.Record("Request body captured", request);
        }

        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            TResponse? response = await next();

            // Capture Response Body (unless sensitive)
            if (IsSensitive(typeof(TResponse)))
            {
                session.Record("Response body redacted (Sensitive)");
            }
            else
            {
                session.Record("Response body captured", response);
            }

            return response;
        }
        catch (Exception ex)
        {
            session.MarkFailed(ex.Message, ex);
            throw;
        }
        finally
        {
            // Close node and save session
            sw.Stop();
            MTraceSessionRecord exportedSession = session.Export();
            await sessionStore.SaveAsync(exportedSession, TimeSpan.FromHours(24), cancellationToken);

            logger.Info("[MEDIATOR] [DIAGNOSTICS] Trace captured for {RequestName}. SessionId: {SessionId}", typeof(TRequest).Name, session.SessionId);
        }
    }

    private ActivationLevel GetActivationLevel()
    {
        if (httpContextAccessor?.HttpContext?.Request.Headers.TryGetValue(TraceHeader, out Microsoft.Extensions.Primitives.StringValues values) == true)
        {
            string value = values.ToString().ToLowerInvariant();
            if (value == "deep")
            {
                return ActivationLevel.Deep;
            }

            if (value == "true")
            {
                return ActivationLevel.Debug;
            }

            if (value == "off")
            {
                return ActivationLevel.None;
            }
        }

        // Fallback or tenant-level check can be added here
        return ActivationLevel.None;
    }

    private static bool IsSensitive(Type type)
    {
        // Placeholder for [MTraceSensitive] attribute check in Phase 4
        _ = type;
        return false;
    }

    private enum ActivationLevel { None, Debug, Deep }
}
