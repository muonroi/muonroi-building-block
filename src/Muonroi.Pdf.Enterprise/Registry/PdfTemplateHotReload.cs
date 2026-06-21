using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Pdf.Enterprise.Registry;

/// <summary>Configuration for <see cref="PdfTemplateHotReload"/>.</summary>
public sealed class PdfTemplateHotReloadOptions
{
    /// <summary>How often to re-check tracked templates for a version change.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The template identifiers to watch. Empty → the subscriber idles until cancelled.</summary>
    public IReadOnlyList<string> TemplateIds { get; set; } = [];
}

/// <summary>
/// Transport-agnostic <see cref="IMPdfTemplateHotReload"/> that polls a set of tracked templates via
/// <see cref="IMPdfTemplateRegistry.LookupAsync"/> and forwards a <see cref="TemplateChange"/> to the
/// supplied observer whenever a template's latest version changes (or it disappears). Works against
/// ANY registry implementation, so hot-reload needs no control-plane push transport. A host wraps
/// <see cref="StartAsync"/> in a background task / hosted service.
/// </summary>
public sealed class PdfTemplateHotReload(
    IMPdfTemplateRegistry registry,
    IAsyncObserver<TemplateChange> observer,
    PdfTemplateHotReloadOptions options,
    IMLog<PdfTemplateHotReload>? logger = null) : IMPdfTemplateHotReload
{
    private readonly IMPdfTemplateRegistry _registry = MGuard.NotNull(registry);
    private readonly IAsyncObserver<TemplateChange> _observer = MGuard.NotNull(observer);
    private readonly PdfTemplateHotReloadOptions _options = MGuard.NotNull(options);

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // templateId → last observed latest-version (null = template absent).
        var lastVersion = new Dictionary<string, string?>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (string templateId in _options.TemplateIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                string? current;
                try
                {
                    TemplateDescriptor? descriptor = await _registry.LookupAsync(templateId, cancellationToken);
                    current = descriptor?.LatestVersion;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Transient lookup failure — log and keep polling; the baseline is unchanged.
                    logger?.Warn("[PdfHotReload] Lookup failed for template {TemplateId}: {Error}", templateId, ex.Message);
                    continue;
                }

                bool hadBaseline = lastVersion.TryGetValue(templateId, out string? previous);
                lastVersion[templateId] = current;
                if (!hadBaseline)
                    continue; // first observation seeds the baseline; emit no event

                if (string.Equals(previous, current, StringComparison.Ordinal))
                    continue;

                TemplateChange change = current is null
                    ? new TemplateChange(templateId, null, TemplateChangeKind.Deleted)
                    : new TemplateChange(templateId, current, TemplateChangeKind.Updated);

                try
                {
                    await _observer.OnNextAsync(change, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger?.Error(ex, "[PdfHotReload] Observer threw handling change for template {TemplateId}.", templateId);
                }
            }

            try
            {
                await Task.Delay(_options.PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        try
        {
            await _observer.OnCompletedAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[PdfHotReload] Observer threw on completion.");
        }
    }
}
