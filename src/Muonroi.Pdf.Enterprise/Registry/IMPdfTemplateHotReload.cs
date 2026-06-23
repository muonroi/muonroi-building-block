namespace Muonroi.Pdf.Enterprise.Registry;

/// <summary>
/// Subscriber interface for template hot-reload events.
/// Implementations listen for registry change notifications and refresh
/// in-process template caches without a process restart.
/// <para>
/// <see cref="PdfTemplateHotReload"/> is the transport-agnostic polling implementation (works against
/// any <see cref="IMPdfTemplateRegistry"/>). A push-based variant (control-plane SignalR/Redis) is a
/// cross-repo transport that can be substituted behind this same interface.
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
