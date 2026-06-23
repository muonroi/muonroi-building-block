using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.Pdf.Abstractions;
using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.Core;

namespace Muonroi.Pdf.Enterprise.Metering;

/// <summary>
/// <see cref="IMPdfService"/> decorator that records per-tenant, per-render quota usage
/// via <see cref="ITenantQuotaTracker.IncrementUsageAsync"/> after each render completes.
/// <para>
/// Never blocks or throws on metering failure (D-02: record-only). A throwing tracker is
/// caught, logged, and swallowed so the render result is always returned to the caller.
/// </para>
/// </summary>
public sealed class EnterprisePdfServiceWrapper(
    IMPdfService inner,
    ITenantQuotaTracker quotaTracker,
    ISystemExecutionContextAccessor? executionContextAccessor = null,
    IMLog<EnterprisePdfServiceWrapper>? logger = null) : IMPdfService
{
    /// <inheritdoc />
    public async Task<PdfRenderResult> RenderAsync(
        string html,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        PdfRenderResult result = await inner.RenderAsync(html, destination, options, cancellationToken);
        await RecordMeteringAsync(result.PageCount, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<PdfRenderResult> RenderMultiPageAsync(
        IReadOnlyList<string> htmlPages,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        PdfRenderResult result = await inner.RenderMultiPageAsync(htmlPages, destination, options, cancellationToken);
        await RecordMeteringAsync(result.PageCount, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<(byte[] Bytes, PdfRenderResult Metadata)> RenderToBytesAsync(
        string html,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        (byte[] bytes, PdfRenderResult metadata) = await inner.RenderToBytesAsync(html, options, cancellationToken);
        await RecordMeteringAsync(metadata.PageCount, cancellationToken);
        return (bytes, metadata);
    }

    /// <summary>
    /// Records one metered event for the current tenant using <see cref="QuotaType.PdfRendersPerDay"/>.
    /// Resolves tenant from <see cref="ISystemExecutionContextAccessor"/> first, then falls back to
    /// <see cref="TenantContext.CurrentTenantId"/> (AsyncLocal ambient context).
    /// If no tenant is resolvable, skips recording silently.
    /// Any exception from <see cref="ITenantQuotaTracker"/> is caught, logged, and swallowed
    /// (T-16-05 mitigation: metering must never become a denial-of-service on the render path).
    /// </summary>
    private async Task RecordMeteringAsync(int pageCount, CancellationToken ct)
    {
        // Mirror EnterpriseLicenseGuardEnhancer: accessor.Get() first, AsyncLocal fallback.
        string? tenantId = executionContextAccessor?.Get()?.TenantId ?? TenantContext.CurrentTenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        try
        {
            await quotaTracker.IncrementUsageAsync(tenantId, QuotaType.PdfRendersPerDay, pageCount, ct);
        }
        catch (Exception ex)
        {
            // No Silent Catch (CLAUDE.md): log error with context, then swallow so the render
            // result is always returned to the caller (D-02: record-only, never blocks).
            logger?.Error(ex, "[PDF] Metering record failed (non-blocking): {Message}", ex.Message);
        }
    }
}
