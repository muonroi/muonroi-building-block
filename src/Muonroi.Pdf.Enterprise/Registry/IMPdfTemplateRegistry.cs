namespace Muonroi.Pdf.Enterprise.Registry;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// Summary descriptor for a template entry in the registry.
/// </summary>
/// <param name="TemplateId">Unique identifier for the template.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="LatestVersion">Version string of the most recent published version.</param>
/// <param name="Tags">Free-form tags for catalogue filtering.</param>
public sealed record TemplateDescriptor(
    string TemplateId,
    string Name,
    string LatestVersion,
    IReadOnlyList<string> Tags);

/// <summary>
/// A specific resolved version of a template, including its content payload.
/// </summary>
/// <param name="TemplateId">Unique identifier for the template.</param>
/// <param name="Version">Resolved version string.</param>
/// <param name="ContentType">MIME type of <see cref="Content"/> (e.g., <c>text/html</c>).</param>
/// <param name="Content">Raw template content bytes.</param>
/// <param name="PublishedAt">UTC timestamp when this version was published.</param>
public sealed record TemplateVersion(
    string TemplateId,
    string Version,
    string ContentType,
    ReadOnlyMemory<byte> Content,
    DateTimeOffset PublishedAt);

/// <summary>
/// A change notification emitted by the registry when a template is updated or deleted.
/// </summary>
/// <param name="TemplateId">The affected template.</param>
/// <param name="NewVersion">
/// The new version string, or <c>null</c> if the template was deleted.
/// </param>
/// <param name="ChangeKind">The kind of change that occurred.</param>
public sealed record TemplateChange(
    string TemplateId,
    string? NewVersion,
    TemplateChangeKind ChangeKind);

/// <summary>Kind of change reported by the template registry.</summary>
public enum TemplateChangeKind
{
    /// <summary>A new version of an existing template was published.</summary>
    Updated,

    /// <summary>The template was removed from the registry.</summary>
    Deleted,
}

// ---------------------------------------------------------------------------
// Observer contract (mirrors System.IObserver<T> for async streams)
// ---------------------------------------------------------------------------

/// <summary>
/// Async observer contract used by <see cref="IMPdfTemplateRegistry.SubscribeAsync"/>.
/// </summary>
public interface IAsyncObserver<in T>
{
    /// <summary>Called for each emitted item.</summary>
    ValueTask OnNextAsync(T value, CancellationToken cancellationToken = default);

    /// <summary>Called when the stream ends with an error.</summary>
    ValueTask OnErrorAsync(Exception error, CancellationToken cancellationToken = default);

    /// <summary>Called when the stream completes normally.</summary>
    ValueTask OnCompletedAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Registry interface
// ---------------------------------------------------------------------------

/// <summary>
/// Client contract for the Muonroi PDF template registry.
/// <para>
/// The REST transport is <see cref="HttpPdfTemplateRegistry"/> (Lookup / Resolve). Push-based
/// change notifications (<see cref="SubscribeAsync"/>) require the control-plane hot-reload transport
/// (SignalR/Redis), which is cross-repo; <see cref="PdfTemplateHotReload"/> polls specific templates
/// over any registry as the building-block-side hot-reload.
/// </para>
/// </summary>
public interface IMPdfTemplateRegistry
{
    /// <summary>
    /// Looks up the summary descriptor for a template by its identifier.
    /// </summary>
    /// <param name="templateId">The unique template identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="TemplateDescriptor"/>, or <c>null</c> if not found.
    /// </returns>
    Task<TemplateDescriptor?> LookupAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a specific version of a template, returning its full content payload.
    /// Pass <c>"latest"</c> as <paramref name="version"/> to resolve the most recent version.
    /// </summary>
    /// <param name="templateId">The unique template identifier.</param>
    /// <param name="version">The version to resolve, or <c>"latest"</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="TemplateVersion"/> with content, or <c>null</c> if not found.
    /// </returns>
    Task<TemplateVersion?> ResolveAsync(string templateId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes an observer to receive <see cref="TemplateChange"/> notifications
    /// whenever a template in the registry is updated or deleted.
    /// The returned <see cref="IAsyncDisposable"/> unsubscribes when disposed.
    /// </summary>
    /// <param name="observer">Observer that will receive change events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IAsyncDisposable> SubscribeAsync(IAsyncObserver<TemplateChange> observer, CancellationToken cancellationToken = default);
}
