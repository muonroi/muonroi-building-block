namespace Quickstart.Observability.Api.Logging;

/// <summary>
/// Shows the <see cref="IMLog{T}"/> structured-logging patterns available in
/// Muonroi.Logging.
///
/// Key API surface demonstrated here:
///
/// <list type="bullet">
///   <item>
///     <see cref="IMLog.BeginProperty"/> — pushes a key/value pair into the
///     ambient log scope.  Every log statement emitted while the returned
///     <see cref="IMLogContextScope"/> is alive carries that property in its
///     structured payload (visible in Seq, Loki, Application Insights, etc.).
///   </item>
///   <item>
///     Convenience methods <c>Info</c>, <c>Warn</c>, <c>Error</c>, <c>Debug</c>
///     as alternatives to the standard <c>LogInformation</c> / … methods.
///   </item>
/// </list>
///
/// This class is intentionally kept as a thin wrapper so it can be injected
/// wherever fine-grained log scoping is needed.
/// </summary>
public sealed class ProductsLogger(IMLog<ProductsLogger> log)
{
    private readonly IMLog<ProductsLogger> _log = log;

    /// <summary>
    /// Logs the start of a product-list operation with a structured scope that
    /// attaches <c>Operation=ListProducts</c> to every log line emitted inside.
    /// </summary>
    public void LogListStarted(int requestedPage, int pageSize)
    {
        // BeginProperty returns an IDisposable scope.  All log calls inside the
        // using block automatically carry the "Operation" property.
        using IMLogContextScope operationScope = _log.BeginProperty("Operation", "ListProducts");
        using IMLogContextScope pageScope      = _log.BeginProperty("Page",      requestedPage);
        using IMLogContextScope sizeScope      = _log.BeginProperty("PageSize",  pageSize);

        _log.Info("Starting product list operation — page {Page}, size {PageSize}",
            requestedPage, pageSize);
    }

    /// <summary>
    /// Logs a successful product retrieval with the resolved product context
    /// attached as structured properties.
    /// </summary>
    public void LogProductFound(int id, string name, string category, double elapsedMs)
    {
        using IMLogContextScope idScope       = _log.BeginProperty("ProductId",       id);
        using IMLogContextScope categoryScope = _log.BeginProperty("ProductCategory", category);

        _log.Info("Product {ProductName} ({ProductId}) retrieved in {ElapsedMs:F2} ms",
            name, id, elapsedMs);
    }

    /// <summary>
    /// Logs a product-not-found warning with structured context so that it can be
    /// queried in log aggregators without string parsing.
    /// </summary>
    public void LogProductNotFound(int id, double elapsedMs)
    {
        using IMLogContextScope idScope = _log.BeginProperty("ProductId", id);

        _log.Warn("Product {ProductId} not found after {ElapsedMs:F2} ms", id, elapsedMs);
    }

    /// <summary>
    /// Logs an unexpected error with full exception details.
    /// The <see cref="IMLog.Error"/> overload accepts the exception as the first
    /// argument, which Serilog (and Microsoft.Extensions.Logging) forward to the
    /// sink for structured exception rendering.
    /// </summary>
    public void LogError(int id, Exception ex)
    {
        using IMLogContextScope idScope = _log.BeginProperty("ProductId", id);

        _log.Error(ex, "Unexpected error while processing product {ProductId}", id);
    }
}
