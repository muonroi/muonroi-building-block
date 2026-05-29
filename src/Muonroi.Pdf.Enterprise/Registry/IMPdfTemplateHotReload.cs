namespace Muonroi.Pdf.Enterprise.Registry;

/// <summary>
/// Subscriber interface for template hot-reload events.
/// Implementations listen for registry change notifications and refresh
/// in-process template caches without a process restart.
/// <para>
/// The Redis pub/sub implementation (<c>RedisPdfTemplateHotReload</c>) lands in Phase 9.2.
/// </para>
/// </summary>
public interface IMPdfTemplateHotReload
{
    /// <summary>
    /// Starts the hot-reload subscriber, connecting to the underlying transport
    /// and processing incoming <see cref="TemplateChange"/> events until the
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that signals the subscriber to stop and clean up.
    /// </param>
    Task StartAsync(CancellationToken cancellationToken = default);
}
